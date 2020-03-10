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
    public static int Main() => Execute<Build>(x => x.Compile);

    [Solution]
    readonly Solution Solution;

    [Parameter("configuration")]
    public string Configuration { get; set; }

    [Parameter("version-suffix")]
    public string VersionSuffix { get; set; }

    [Parameter("publish-framework")]
    public string PublishFramework { get; set; }

    [Parameter("publish-runtime")]
    public string PublishRuntime { get; set; }

    [Parameter("publish-project")]
    public string PublishProject { get; set; }

    AbsolutePath SourceDirectory => RootDirectory / "src";

    //AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    protected override void OnBuildInitialized()
    {
        Configuration ??= "Release";
        VersionSuffix ??= string.Empty;
    }

    private void DeleteDirectories(IReadOnlyCollection<string> directories)
    {
        foreach (var directory in directories)
        {
            DeleteDirectory(directory);
        }
    }

    Target Clean => _ => _
        .Executes(() =>
        {
            DeleteDirectories(GlobDirectories(SourceDirectory, "**/bin", "**/obj"));
            //EnsureCleanDirectory(ArtifactsDirectory);
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetVersionSuffix(VersionSuffix)
                .EnableNoRestore());
        });

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
