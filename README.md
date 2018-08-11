# RepositoryAnaltyicsOrchestrator

A .NET Core console application to orchestrate the refreshing of repository analytics information via the [RepositoryAnaltyicsApi](https://github.com/Firenza/RepositoryAnaltyicsApi)

#### Purpose

Serve as an orchestrator for doing bulk operations via calls to the [RepositoryAnaltyicsApi](https://github.com/Firenza/RepositoryAnaltyicsApi). This allows the API to not have to handle bite sized chunks of work and not have to worry about long running tasks. Currently the following workloads are supported.

* Iterate over all repositories and analyze them via a request to the API. 

#### Tech Used

* .NET Core console application

#### Running locally

The available command line arguments are documented [here](https://github.com/Firenza/RepositoryAnaltyicsDataRefresher/blob/8a2cce7c4da85958e9737dd8752a4e1df00f60b2/src/RepositoryAnaltyicsDataRefresher/Program.cs?#L19-#L42)

###### Using Visual Studio

1. Open the solution and go to the project properties of the `RepositoryAnaltyicsDataRefresher` project
2. Click the Debug Tab
3. In the `Application Arguments` section enter in your command line arguments
4. Hit `F5` or click the button with the green triangle.

###### Via command line

1. Open a terminal window in the `\src\` diretory
2. Run the following command to build the application 

`dotnet build`

3. Run the following command to start the application. You will need to provide the command line arguments at the end of the command.

`dotnet .\RepositoryAnaltyicsDataRefresher\bin\Debug\netcoreapp2.0\RepositoryAnaltyicsDataRefresher.dll`
