global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
global using Task = System.Threading.Tasks.Task;
using System.Runtime.InteropServices;
using System.Threading;

// =============================================================================
// Todo: - Command to make banners (`///`-style, 1 line (framed, so 3 total),
//         with heading between asterisms). Would place the cursor between
//         asterisms after making it.
//       - Command to cycle through banners
//       - EOL, i.e. `// Content [padding spaces] //`?
// =============================================================================
namespace CommentWrap
{
	[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
	[ProvideMenuResource("Menus.ctmenu", 1)]
	[Guid(PackageGuids.CommentWrapString)]
	public sealed class Package : ToolkitPackage
	{
		protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
		{
			await this.RegisterCommandsAsync();
		}
	}
}
