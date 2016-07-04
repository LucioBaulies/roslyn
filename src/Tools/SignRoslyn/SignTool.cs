using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace SignRoslyn
{
    internal sealed class SignTool : ISignTool
    {
        private readonly string _msbuildPath;
        private readonly string _binariesPath;
        private readonly string _sourcePath;
        private readonly string _buildFilePath;
        private readonly string _runPath;
        private bool _generatedBuildFile;

        internal SignTool(string runPath, string binariesPath, string sourcePath)
        {
            _binariesPath = binariesPath;
            _sourcePath = sourcePath;
            _buildFilePath = Path.Combine(runPath, "build.proj");
            _runPath = runPath;

            var path = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            _msbuildPath = Path.Combine(path, @"MSBuild\14.0\Bin\MSBuild.exe");
            if (!File.Exists(_msbuildPath))
            {
                throw new Exception(@"Unable to locate MSBuild at the path {_msbuildPath}");
            }
        }

        private void Sign(IEnumerable<string> filePaths)
        {
            var commandLine = new StringBuilder();
            commandLine.Append(@"/v:m /target:RoslynSign ");
            commandLine.Append(@""" ");
            commandLine.Append(Path.GetFileName(_buildFilePath));
            Console.WriteLine($"msbuild.exe {commandLine.ToString()}");

            var content = GenerateBuildFileContent(filePaths);
            File.WriteAllText(_buildFilePath, content);
            Console.WriteLine("Generated project file");
            Console.WriteLine(content);

            var startInfo = new ProcessStartInfo()
            {
                FileName = _msbuildPath,
                Arguments = commandLine.ToString(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                WorkingDirectory = _runPath,
            };

            var process = Process.Start(startInfo);
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                Console.WriteLine("MSBuild failed!!!");
                Console.WriteLine(process.StandardOutput.ReadToEnd());
                throw new Exception("Sign failed");
            }
        }

        private string GenerateBuildFileContent(IEnumerable<string> filesToSign)
        {
            var builder = new StringBuilder();
            builder.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>
<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">");

            builder.AppendLine($@"    <Import Project=""{Path.Combine(_sourcePath, @"build\Targets\VSL.Settings.targets")}"" />");

            builder.AppendLine($@"
    <Import Project=""$(NuGetPackageRoot)\MicroBuild.Core\0.2.0\build\MicroBuild.Core.props"" />
    <Import Project=""$(NuGetPackageRoot)\MicroBuild.Core\0.2.0\build\MicroBuild.Core.targets"" />");

            builder.Append($@"    <ItemGroup>");

            foreach (var fileToSign in filesToSign)
            {
                builder.Append($@"
        <FilesToSign Include=""{fileToSign}"">
          <Authenticode>$(AuthenticodeCertificateName)</Authenticode>
          <StrongName>MsSharedLib72</StrongName>
        </FilesToSign>");
            }

            builder.Append($@"    </ItemGroup>");

            builder.AppendLine($@"
    <Target Name=""RoslynSign"">

        <SignFiles Files=""@(FilesToSign)""
                   BinariesDirectory=""{_binariesPath}""
                   IntermediatesDirectory=""{Path.GetTempPath()}""
                   Type=""real"" />
    </Target>
</Project>");

            return builder.ToString();
        }

        void ISignTool.Sign(IEnumerable<string> filePaths)
        {
            Sign(filePaths);
        }
    }
}
