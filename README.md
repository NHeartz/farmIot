# My ASP.NET Core HiveMQ Project

This project is an ASP.NET Core application that connects to HiveMQ to receive sensor data and store it in a PostgreSQL database. The application is structured to handle incoming MQTT messages, process them, and save the relevant data into a `SensorData` table.

## Project Structure

- **src/MyAspNetCoreHiveMQ**: Contains the main application code.
  - **Controllers**: Contains the `SensorController` for handling HTTP requests related to sensor data.
  - **Services**: Contains the `HiveMqttService` for connecting to HiveMQ and processing messages.
  - **Data**: Contains the `ApplicationDbContext` for database interactions.
  - **Models**: Contains the `SensorData` model representing the database structure.
  - **Properties**: Contains launch settings for the application.
  - **appsettings.json**: Configuration settings for the application.
  - **Program.cs**: Entry point of the application.
  - **MyAspNetCoreHiveMQ.csproj**: Project file specifying dependencies and settings.

- **tests/MyAspNetCoreHiveMQ.Tests**: Contains unit tests for the application.
  - **SensorServiceTests.cs**: Tests for the `HiveMqttService`.

- **.gitignore**: Specifies files and directories to be ignored by version control.

- **Dockerfile**: Instructions for building a Docker image for the application.

## Setup Instructions

1. **Clone the Repository**:
   ```bash
   git clone <repository-url>
   cd my-aspnetcore-hivemq
   ```

2. **Install Dependencies**:
   Make sure you have the .NET SDK installed. Run the following command to restore the dependencies:
   ```bash
   dotnet restore
   ```

3. **Configure Database**:
   Update the `appsettings.json` file with your PostgreSQL connection string.

4. **Run Migrations**:
   Ensure your database is set up and run the migrations to create the necessary tables:
   ```bash
   dotnet ef database update
   ```

5. **Run the Application**:
   Start the application using:
   ```bash
   dotnet run
   ```

6. **Testing**:
   To run the unit tests, navigate to the `tests/MyAspNetCoreHiveMQ.Tests` directory and run:
   ```bash
   dotnet test
   ```

## Usage

The application listens for incoming MQTT messages from HiveMQ. When a message is received, it is processed and the relevant sensor data is stored in the PostgreSQL database.

## Contributing

Contributions are welcome! Please open an issue or submit a pull request for any improvements or bug fixes.

## License

This project is licensed under the MIT License. See the LICENSE file for more details.