#addin "Cake.FileHelpers"

var target = Argument("target", "Default");
var buildInAppveyor = bool.Parse(EnvironmentVariable("APPVEYOR") ?? "False");
var manualBuild = bool.Parse(EnvironmentVariable("APPVEYOR_FORCED_BUILD") ?? "False");
var slnPath = "./../CTime2.sln";
var verbosity = (Verbosity)Enum.Parse(typeof(Verbosity), Argument("verbosity", "Verbose"));
var versionNumber = (EnvironmentVariable("APPVEYOR_BUILD_VERSION") ?? "9999.9999.9999.0").Split('-')[0];

Task("CleanFolders")
    .Does(() => 
{
    var paths = new string[] 
    {
        "./../src/CTime2/",
        "./../src/CTime2.Core/",
        "./../src/CTime2.LiveTileService/",
        "./../src/CTime2.VoiceCommandService/",
    };

    foreach (var path in paths)
    {
        CleanDirectory(path + "bin");
        CleanDirectory(path + "obj");
    }

    CleanDirectory("./../src/CTime2/AppPackages");
    CleanDirectory("./../src/CTime2/BundleArtifacts");
});

Task("UpdateAppxManifestVersion")
    .WithCriteria(() => buildInAppveyor && manualBuild)
    .IsDependentOn("CleanFolders")
    .Does(() => 
{
    var appxManifestPath = "./../src/CTime2/Package.appxmanifest";

    var appxManifestContent = FileReadText(appxManifestPath);
    appxManifestContent = appxManifestContent.Replace("9999.9999.9999.0", versionNumber);
    FileWriteText(appxManifestPath, appxManifestContent);
});

Task("NuGetRestore")
    .IsDependentOn("UpdateAppxManifestVersion")
    .Does(() => 
{
    NuGetRestore(slnPath);
});

Task("Build")
    .IsDependentOn("NugetRestore")
    .Does(() => 
{
    MSBuildSettings settings;
    if (buildInAppveyor && manualBuild)
    {
        settings = new MSBuildSettings 
        {
            Configuration = "Release",
            Verbosity = verbosity,
            ToolVersion = MSBuildToolVersion.VS2015,
            EnvironmentVariables = new Dictionary<string, string> 
            {
                { "AppxBundlePlatforms", "x86|x64|ARM" },
                { "AppxPackageDir", "AppPackages" },
                { "AppxBundle", "Always" },
                { "UapAppxPackageBuildMode", "StoreUpload" },
            }
        };
    }
    else
    {
        settings = new MSBuildSettings 
        {
            Configuration = "Debug",
            Verbosity = verbosity,
            ToolVersion = MSBuildToolVersion.VS2015,
        };
    }

    MSBuild(slnPath, settings);
});

Task("Default")
    .IsDependentOn("Build");

RunTarget(target);