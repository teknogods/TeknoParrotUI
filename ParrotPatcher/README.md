# Parrot Patcher

Parrot Patcher is an application designed to manage and facilitate updates for the TeknoParrot emulator. This project is built using Avalonia, a cross-platform UI framework for .NET.

## Project Structure

The project consists of the following key components:

- **Assets**: Contains application assets such as icons.
- **Models**: Contains data models, including the `UpdaterModel` which handles update-related data and logic.
- **ViewModels**: Contains view models that implement the logic for the UI, including `MainWindowViewModel` for the main window.
- **Views**: Contains XAML files for the UI layout and their corresponding code-behind files.
- **Components**: Contains helper methods and components used throughout the application.
- **App.axaml**: Defines application-level resources and styles.
- **Program.cs**: The entry point of the application.

## Setup Instructions

1. **Clone the Repository**: 
   ```bash
   git clone https://github.com/YourUsername/ParrotPatcher.git
   cd ParrotPatcher
   ```

2. **Install Dependencies**: 
   Make sure you have the .NET 9.0 SDK installed. You can install the necessary packages using:
   ```bash
   dotnet restore
   ```

3. **Build the Project**: 
   To build the project, run:
   ```bash
   dotnet build
   ```

4. **Run the Application**: 
   To run the application, use:
   ```bash
   dotnet run
   ```

## Usage

Once the application is running, you can use it to manage updates for the TeknoParrot emulator. The main window provides options to check for updates, download them, and apply them as needed.

## Contributing

Contributions are welcome! Please feel free to submit a pull request or open an issue for any enhancements or bug fixes.

## License

This project is licensed under the MIT License. See the LICENSE file for more details.