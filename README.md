# PenguQuiz

Web-based quiz application focused on knowledge training and competitive gameplay.

## Author

`Author: Pavel Halík`

## Project Overview

PenguQuiz is a web quiz application developed as a school project. It allows users to test their knowledge through interactive quizzes in both casual and competitive modes.

The application includes:

- casual and ranked game modes
- user authentication system
- score tracking and statistics
- questions loaded from external API
- custom quiz mode (user-created quizzes)

The system is designed using a client-server architecture and supports real-time communication.



## Features

### Game Modes

**Casual mode**
- category and difficulty selection
- questions loaded from external API

**Ranked mode**
- competitive gameplay
- results stored in database
- player statistics tracking

### User System

- registration and login
- persistent user data
- tracking of results and progress

### Custom Quiz Mode

- create your own questions
- define answers
- play custom quizzes


## Architecture

The application is divided into two main parts:

### Frontend
- Blazor WebAssembly
- handles UI and user interaction

### Backend
- ASP.NET Core Web API
- business logic
- database communication

### Additional Technologies

- SignalR for real-time communication (multiplayer features)
- Microsoft SQL Server for data storage



## Technologies Used

- C#
- ASP.NET Core Web API
- Blazor WebAssembly
- SignalR
- Microsoft SQL Server
- HTML / CSS



## Requirements

- .NET SDK 6 or higher
- Microsoft SQL Server (Express or higher)
- Visual Studio or Visual Studio Code
- Internet connection (for external API)
- Modern web browser



## Deployment

### Backend

```bash
dotnet publish -c Release
dotnet ./publish/QuizAPI.dll
```
### Frontend
```bash
dotnet publish -c Release
```

## Contact

If you need anything from me about this application contact me at:

* pavel.halik06@gmail.com
