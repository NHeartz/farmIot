#include <DHT.h>
#include <WiFi.h>
#include <WiFiClientSecure.h>
#include <PubSubClient.h>
#include <ArduinoJson.h>
#include <ModbusMaster.h> // --- เพิ่ม: ไลบรารีสำหรับเซ็นเซอร์ RS485 ---

// ==========================================
// 1. ตั้งค่า Wi-Fi และ EMQX
// ==========================================
const char* WIFI_SSID = "4212 2.4G";
const char* WIFI_PASSWORD = "1234567890";

const char* MQTT_HOST = "u1681021.ala.asia-southeast1.emqxsl.com";
const int MQTT_PORT = 8883;
const char* MQTT_USERNAME = "NHeartz";
const char* MQTT_PASSWORD = "1234567890";
const char* MQTT_CLIENT_ID = "ESP-Farm-Plot1";

const char* COMMAND_TOPIC = "farm/plot1/valve/control";
const char* STATUS_TOPIC = "farm/plot1/valve/status";
const char* AVAILABILITY_TOPIC = "farm/plot1/valve/availability";

// Existing sensor wiring.
constexpr int DHT_PIN = 23;
constexpr int SOIL_PIN = 36; // ยังคงเซ็นเซอร์ความชื้นดินแบบ Analog ไว้ (ถ้าไม่ได้ใช้แล้วลบออกได้)
constexpr int FLOW_PIN = 27;
constexpr int RELAY_PIN = 14;

// --- เพิ่ม: ตั้งค่าขา RS485 (Modbus) ---
constexpr int RX2_PIN = 16;
constexpr int TX2_PIN = 17;

constexpr auto DHT_TYPE = DHT11;
constexpr float FLOW_CALIBRATION_FACTOR = 4.5f;

// This sketch assumes an active-low relay board and a normally-closed valve.
constexpr int RELAY_ON = LOW;
constexpr int RELAY_OFF = HIGH;

constexpr uint32_t SENSOR_INTERVAL_MS = 2000;
constexpr uint32_t FLOW_INTERVAL_MS = 1000;
constexpr uint32_t WIFI_RETRY_INTERVAL_MS = 10000;
constexpr uint32_t MQTT_RETRY_INTERVAL_MS = 5000;
constexpr uint32_t MAX_WATERING_SECONDS = 86400;
constexpr size_t RECENT_JOB_COUNT = 8;

WiFiClientSecure secureClient;
PubSubClient mqttClient(secureClient);
DHT dht(DHT_PIN, DHT_TYPE);
ModbusMaster node; // --- เพิ่ม: สร้าง Object สำหรับเซ็นเซอร์ RS485 ---

uint32_t lastSensorReadAt = 0;
uint32_t lastFlowReadAt = 0;
uint32_t lastWifiAttemptAt = 0;
uint32_t lastMqttAttemptAt = 0;
volatile uint32_t flowPulseCount = 0;
float flowRateLitersPerMinute = 0.0f;
portMUX_TYPE flowMux = portMUX_INITIALIZER_UNLOCKED;

bool watering = false;
bool wifiWasConnected = false;
uint64_t activeJobId = 0;
uint32_t wateringStopsAt = 0;

// Cache terminal jobs so a duplicate MQTT delivery does not reopen the valve.
uint64_t recentJobIds[RECENT_JOB_COUNT] = {};
char recentJobStatuses[RECENT_JOB_COUNT][12] = {};
size_t recentJobCursor = 0;

// Hold the last terminal event until MQTT reconnects.
bool hasPendingTerminalEvent = false;
uint64_t pendingTerminalJobId = 0;
char pendingTerminalStatus[12] = {};
char pendingTerminalMessage[48] = {};

void IRAM_ATTR onFlowPulse() {
  portENTER_CRITICAL_ISR(&flowMux);
  ++flowPulseCount;
  portEXIT_CRITICAL_ISR(&flowMux);
}

void publishStatus(uint64_t jobId, const char* status, const char* detail = "") {
  if (!mqttClient.connected()) {
    return;
  }

  StaticJsonDocument<192> document;
  document["jobId"] = jobId;
  document["status"] = status;
  if (detail[0] != '\0') {
    document["detail"] = detail;
  }

  char payload[192];
  const size_t length = serializeJson(document, payload, sizeof(payload));
  if (length > 0) {
    const bool sent = mqttClient.publish(STATUS_TOPIC, payload, false);
    Serial.printf("Valve status %s: jobId=%llu status=%s\n",
                  sent ? "published" : "publish failed",
                  static_cast<unsigned long long>(jobId), status);
  }
}

bool findRecentJob(uint64_t jobId, const char** status) {
  for (size_t i = 0; i < RECENT_JOB_COUNT; ++i) {
    if (recentJobIds[i] == jobId) {
      *status = recentJobStatuses[i];
      return true;
    }
  }
  return false;
}

void rememberFinishedJob(uint64_t jobId, const char* status) {
  recentJobIds[recentJobCursor] = jobId;
  strlcpy(recentJobStatuses[recentJobCursor], status,
          sizeof(recentJobStatuses[recentJobCursor]));
  recentJobCursor = (recentJobCursor + 1) % RECENT_JOB_COUNT;
}

void setPendingTerminalEvent(uint64_t jobId, const char* status,
                             const char* detail) {
  hasPendingTerminalEvent = true;
  pendingTerminalJobId = jobId;
  strlcpy(pendingTerminalStatus, status, sizeof(pendingTerminalStatus));
  strlcpy(pendingTerminalMessage, detail, sizeof(pendingTerminalMessage));
}

void flushPendingTerminalEvent() {
  if (!hasPendingTerminalEvent || !mqttClient.connected()) {
    return;
  }

  StaticJsonDocument<192> document;
  document["jobId"] = pendingTerminalJobId;
  document["status"] = pendingTerminalStatus;
  if (pendingTerminalMessage[0] != '\0') {
    document["detail"] = pendingTerminalMessage;
  }

  char payload[192];
  const size_t length = serializeJson(document, payload, sizeof(payload));
  if (length > 0 && mqttClient.publish(STATUS_TOPIC, payload, false)) {
    hasPendingTerminalEvent = false;
  }
}

void finishWatering(const char* status, const char* detail) {
  if (!watering) {
    return;
  }

  digitalWrite(RELAY_PIN, RELAY_OFF);

  const uint64_t finishedJobId = activeJobId;
  watering = false;
  activeJobId = 0;
  wateringStopsAt = 0;

  rememberFinishedJob(finishedJobId, status);
  setPendingTerminalEvent(finishedJobId, status, detail);
  flushPendingTerminalEvent();
}

void handleCommand(char* topic, byte* payload, unsigned int length) {
  if (strcmp(topic, COMMAND_TOPIC) != 0 || length >= 256) {
    return;
  }

  char message[256];
  memcpy(message, payload, length);
  message[length] = '\0';
  Serial.printf("MQTT command on %s: %s\n", topic, message);

  StaticJsonDocument<192> document;
  if (deserializeJson(document, message)) {
    publishStatus(0, "rejected", "invalid JSON command");
    return;
  }

  const uint64_t jobId = document["jobId"] | static_cast<uint64_t>(0);
  const char* action = document["action"] | "";
  if (jobId == 0) {
    publishStatus(0, "rejected", "missing jobId");
    return;
  }

  if (strcmp(action, "stop") == 0) {
    if (watering && jobId == activeJobId) {
      finishWatering("stopped", "stop command received");
    } else {
      const char* previousStatus = nullptr;
      if (findRecentJob(jobId, &previousStatus)) {
        publishStatus(jobId, previousStatus, "already finished");
      } else {
        publishStatus(jobId, "rejected", "job is not active");
      }
    }
    return;
  }

  if (strcmp(action, "start") != 0) {
    publishStatus(jobId, "rejected", "action must be start or stop");
    return;
  }

  const char* previousStatus = nullptr;
  if (findRecentJob(jobId, &previousStatus)) {
    publishStatus(jobId, previousStatus, "duplicate job ignored");
    return;
  }

  const uint32_t durationSeconds = document["durationSeconds"] | 0;
  if (durationSeconds == 0 || durationSeconds > MAX_WATERING_SECONDS) {
    publishStatus(jobId, "rejected", "durationSeconds out of range");
    return;
  }

  if (watering) {
    if (jobId == activeJobId) {
      publishStatus(jobId, "started", "job already active");
    } else {
      publishStatus(jobId, "rejected", "another watering job is active");
    }
    return;
  }

  activeJobId = jobId;
  watering = true;
  wateringStopsAt = millis() + durationSeconds * 1000UL;
  digitalWrite(RELAY_PIN, RELAY_ON);
  publishStatus(jobId, "started");
}

void onMqttMessage(char* topic, byte* payload, unsigned int length) {
  handleCommand(topic, payload, length);
}

void connectMqttIfNeeded(uint32_t now) {
  if (watering || WiFi.status() != WL_CONNECTED || mqttClient.connected() ||
      now - lastMqttAttemptAt < MQTT_RETRY_INTERVAL_MS) {
    return;
  }

  lastMqttAttemptAt = now;
  if (mqttClient.connect(MQTT_CLIENT_ID, MQTT_USERNAME, MQTT_PASSWORD,
                         AVAILABILITY_TOPIC, 1, true, "offline")) {
    const bool subscribed = mqttClient.subscribe(COMMAND_TOPIC, 1);
    mqttClient.publish(AVAILABILITY_TOPIC, "online", true);
    flushPendingTerminalEvent();
    Serial.printf("MQTT connected; command subscription %s (%s)\n",
                  subscribed ? "succeeded" : "failed", COMMAND_TOPIC);
  } else {
    Serial.printf("MQTT connection failed, state=%d\n", mqttClient.state());
  }
}

void updateFlowRate(uint32_t now) {
  const uint32_t elapsedMs = now - lastFlowReadAt;
  if (elapsedMs < FLOW_INTERVAL_MS) {
    return;
  }

  portENTER_CRITICAL(&flowMux);
  const uint32_t pulses = flowPulseCount;
  flowPulseCount = 0;
  portEXIT_CRITICAL(&flowMux);

  flowRateLitersPerMinute =
      (pulses * 1000.0f / elapsedMs) / FLOW_CALIBRATION_FACTOR;
  lastFlowReadAt = now;
}

void publishSensor(const char* topic, float value, int decimals = 1) {
  if (!mqttClient.connected() || isnan(value)) {
    return;
  }

  char payload[20];
  dtostrf(value, 0, decimals, payload);
  mqttClient.publish(topic, payload, false);
}

// โอเวอร์โหลดฟังก์ชันสำหรับส่งค่าจำนวนเต็ม (Integer)
void publishSensorInt(const char* topic, int value) {
  if (!mqttClient.connected()) {
    return;
  }
  char payload[20];
  snprintf(payload, sizeof(payload), "%d", value);
  mqttClient.publish(topic, payload, false);
}

void readAndPublishSensors(uint32_t now) {
  if (now - lastSensorReadAt < SENSOR_INTERVAL_MS) {
    return;
  }
  lastSensorReadAt = now;

  // 1. อ่านค่า DHT และ Analog เดิม
  const float humidity = dht.readHumidity();
  const float temperature = dht.readTemperature();
  const int soilRawValue = analogRead(SOIL_PIN);
  const int soilPercent = constrain(map(soilRawValue, 4095, 0, 0, 100), 0, 100);

  publishSensor("farm/plot1/temp", temperature, 1);
  publishSensor("farm/plot1/air_humid", humidity, 1);
  publishSensor("farm/plot1/water_flow", flowRateLitersPerMinute, 1);
  if (mqttClient.connected()) {
    publishSensorInt("farm/plot1/soil_humid", soilPercent);
  }

  // --- 2. เพิ่ม: อ่านและส่งค่าเซ็นเซอร์ดิน RS485 (Modbus) ---
  uint8_t result = node.readHoldingRegisters(0x0000, 7);
  if (result == node.ku8MBSuccess) {
    uint16_t ec = node.getResponseBuffer(2);
    float ph = node.getResponseBuffer(3) / 10.0f;
    uint16_t nitrogen = node.getResponseBuffer(4);
    uint16_t phosphorus = node.getResponseBuffer(5);
    uint16_t potassium = node.getResponseBuffer(6);

    // ส่งค่าทั้งหมดขึ้น MQTT
    publishSensorInt("farm/plot1/soil_ec", ec);
    publishSensor("farm/plot1/soil_ph", ph, 1);
    publishSensorInt("farm/plot1/soil_n", nitrogen);
    publishSensorInt("farm/plot1/soil_p", phosphorus);
    publishSensorInt("farm/plot1/soil_k", potassium);

    Serial.printf("Modbus Success -> EC: %d, pH: %.1f, N: %d, P: %d, K: %d\n", ec, ph, nitrogen, phosphorus, potassium);
  } else {
    Serial.printf("Modbus Error: 0x%02X\n", result);
  }
}

void setup() {
  Serial.begin(115200);

  // --- เพิ่ม: เริ่มต้นเชื่อมต่อ Serial2 สำหรับ RS485 ---
  Serial2.begin(4800, SERIAL_8N1, RX2_PIN, TX2_PIN);
  node.begin(1, Serial2);

  // Set the output latch to OFF before configuring the pin as an output.
  digitalWrite(RELAY_PIN, RELAY_OFF);
  pinMode(RELAY_PIN, OUTPUT);

  dht.begin();
  analogReadResolution(12);
  pinMode(FLOW_PIN, INPUT_PULLUP);
  attachInterrupt(digitalPinToInterrupt(FLOW_PIN), onFlowPulse, FALLING);

  WiFi.mode(WIFI_STA);
  WiFi.setAutoReconnect(true);
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
  Serial.printf("Connecting to Wi-Fi SSID: %s\n", WIFI_SSID);

  // Testing only. Production firmware should validate the broker certificate
  // with secureClient.setCACert(...).
  secureClient.setInsecure();

  mqttClient.setServer(MQTT_HOST, MQTT_PORT);
  mqttClient.setCallback(onMqttMessage);
  mqttClient.setBufferSize(512);
  mqttClient.setSocketTimeout(1);
  lastFlowReadAt = millis();
}

void loop() {
  const uint32_t now = millis();
  const bool wifiConnected = WiFi.status() == WL_CONNECTED;

  if (wifiConnected && !wifiWasConnected) {
    wifiWasConnected = true;
    Serial.printf("Wi-Fi connected, IP=%s\n", WiFi.localIP().toString().c_str());
  } else if (!wifiConnected && wifiWasConnected) {
    wifiWasConnected = false;
    Serial.println("Wi-Fi disconnected");
  }

  if (!wifiConnected &&
      now - lastWifiAttemptAt >= WIFI_RETRY_INTERVAL_MS) {
    lastWifiAttemptAt = now;
    WiFi.reconnect();
  }

  connectMqttIfNeeded(now);
  updateFlowRate(now);
  if (mqttClient.connected()) {
    mqttClient.loop();
    flushPendingTerminalEvent();
    readAndPublishSensors(now);
  }

  // Check on every loop pass. The local cutoff works even if Backend or MQTT
  // disconnects after the start command.
  if (watering && static_cast<int32_t>(now - wateringStopsAt) >= 0) {
    finishWatering("completed", "duration elapsed");
  }
}