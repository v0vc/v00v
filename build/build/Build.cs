using System.Collections.Generic;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

class Build : NukeBuild
{
    #region Static and Readonly Fields

    [Solution]
    readonly Solution Solution;

    #endregion

    #region Properties

    [Parameter("configuration")] public string Configuration { get; set; }

    [Parameter("publish-framework")] public string PublishFramework { get; set; }

    [Parameter("publish-project")] public string PublishProject { get; set; }

    [Parameter("publish-runtime")] public string PublishRuntime { get; set; }

    [Parameter("version-suffix")] public string VersionSuffix { get; set; }

    private static Target Clean =>
        _ => _.Executes(() =>
        {
            DeleteDirectories(GlobDirectories(SourceDirectory, "**/bin", "**/obj"));
            //EnsureCleanDirectory(ArtifactsDirectory);
        });

    private Target Compile =>
        _ => _.DependsOn(Restore).Executes(() =>
        {
            DotNetBuild(s => s.SetProjectFile(Solution).SetConfiguration(Configuration).SetVersionSuffix(VersionSuffix)
                            .EnableNoRestore());
        });

    private Target Restore =>
        _ => _.DependsOn(Clean).Executes(() =>
        {
            DotNetRestore(s => s.SetProjectFile(Solution));
        });

    private static AbsolutePath SourceDirectory => RootDirectory / "src";

    #endregion

    #region Static Methods

    public static int Main() => Execute<Build>(x => x.Compile);

    #endregion

    #region Methods

    //AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    protected override void OnBuildInitialized()
    {
        Configuration ??= "Release";
        VersionSuffix ??= string.Empty;
    }

    private static void DeleteDirectories(IEnumerable<string> directories)
    {
        foreach (var directory in directories)
        {
            DeleteDirectory(directory);
        }
    }

    #endregion

    //Target Publish => _ => _
    //    .DependsOn(Compile)
    //    .Requires(() => PublishRuntime)
    //    .Requires(() => PublishFramework)
    //    .Requires(() => PublishProject)
    //    .Executes(() =>
    //    {
    //        DotNetPublish(s => s.SetProject(Solution.GetProject(PublishProject)).SetConfiguration(Configuration)
    //                          .SetVersionSuffix(VersionSuffix).SetFramework(PublishFramework).SetRuntime(PublishRuntime));
    //        //.SetOutput(ArtifactsDirectory / "Publish" / PublishProject + "-" + PublishFramework + "-" + PublishRuntime));
    //    });
}
