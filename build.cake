var target = Argument("target", "default");
var rootPath     = "./";
var srcPath      = rootPath + "src/";
var contractPath = rootPath + "contract/";
var testPath     = rootPath + "test/";
var distPath     = rootPath + "aelf-node/";
var solution     = rootPath + "AElf.sln";
var srcProjects  = GetFiles(srcPath + "**/*.csproj");
var contractProjects  = GetFiles(contractPath + "**/*.csproj");

Task("clean")
    .Description("clean up project cache")
    .Does(() =>
{
    DeleteFiles(distPath + "*.nupkg");
    CleanDirectories(srcPath + "**/bin");
    CleanDirectories(srcPath + "**/obj");
    CleanDirectories(contractPath + "**/bin");
    CleanDirectories(contractPath + "**/obj");
    CleanDirectories(testPath + "**/bin");
    CleanDirectories(testPath + "**/obj");
});

Task("restore")
    .Description("restore project dependencies")
    .Does(() =>
{
    var restoreSettings = new DotNetCoreRestoreSettings{
        ArgumentCustomization = args => {
            return args.Append("-v quiet");}
};
    DotNetCoreRestore(solution,restoreSettings);
});

Task("build")
    .Description("Compilation project")
    .IsDependentOn("clean")
    .IsDependentOn("restore")
    .Does(() =>
{
    var buildSetting = new DotNetCoreBuildSettings{
        NoRestore = true,
        Configuration = "Debug",
        ArgumentCustomization = args => {
            return args.Append("/clp:ErrorsOnly")
                       .Append("/p:GeneratePackageOnBuild=false")
                       .Append("-v quiet");}
    };
     
    DotNetCoreBuild(solution, buildSetting);
});


Task("test")
    .Description("operation test")
    .Does(() =>
{
    var testSetting = new DotNetCoreTestSettings{
        NoRestore = true,
        NoBuild = true,
        ArgumentCustomization = args => {
            return args.Append("/p:CollectCoverage=true")
                       .Append("/p:CoverletOutputFormat=json%2copencover")
                       .Append("/p:CoverletOutput=../results/coverage")
                       .Append("/p:MergeWith=../results/coverage.json")
                       .Append("/p:Exclude=[coverlet.*.tests?]*%2c[xunit.*]*%2c[AElf.Kernel.Consensus.Scheduler.*]*%2c[AElf.Database]AElf.Database.RedisProtocol.*%2c[AElf.Contracts.Authorization]*%2c[AElf.Test.Helpers]*%2c[*]*Exception%2c[*.Tests]*%2c[AElf.Contracts.TestContract.BasicFunctionWithParallel]*%2c[AElf.Contracts.GenesisUpdate]*")
                       .Append("/p:Exclude=[*Tests]*%2c[*TestContract*]*%2c[*TestBase*]*")
                       .Append("/p:ExcludeByFile=../../src/AElf.Runtime.CSharp.Core/Metadata/*.cs%2c../../src/AElf.Kernel.SmartContract/Metadata/*.cs%2c../../src/AElf.Database/RedisDatabase.cs");}                   
};
    var testProjects = GetFiles("./test/*.Tests/*.csproj");

    foreach(var testProject in testProjects)
    {
        DotNetCoreTest(testProject.FullPath, testSetting);
    }
});

Task("default")
    .Description("default run test(-target test)")
    .IsDependentOn("build");

RunTarget(target);
