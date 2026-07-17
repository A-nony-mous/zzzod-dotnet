using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZzzOd.Gui.Architecture;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// GUI AXAML 页面基础架构测试。
/// </summary>
public sealed class GuiArchitectureFoundationTests
{
	private sealed class TestPage : ZzzPageView
	{
		public TestPage(TestPageViewModel viewModel)
			: base(viewModel)
		{
		}
	}

	private sealed class TestPageViewModel : ZzzPageViewModel
	{
		public int ShownCount { get; private set; }

		public int LeaveCount { get; private set; }

		public int HiddenCount { get; private set; }

		public int DisposeCount { get; private set; }

		public override void OnPageShown()
		{
			base.OnPageShown();
			ShownCount++;
		}

		public override void OnPageLeave()
		{
			LeaveCount++;
		}

		public override void OnPageHidden()
		{
			HiddenCount++;
		}

		protected override void DisposePageCore()
		{
			DisposeCount++;
		}
	}

	/// <summary>
	/// 页面应绑定 DI 创建的 ViewModel。
	/// </summary>
	[Fact]
	public void AxamlPageRegistrationResolvesViewAndViewModel()
	{
		GuiParityAndFacadeTests.RunOnUiThread(delegate
		{
			ServiceCollection services = new ServiceCollection();
			services.AddZzzAxamlPage<TestPage, TestPageViewModel>();
			using ServiceProvider provider = services.BuildServiceProvider();
			TestPage requiredService = provider.GetRequiredService<TestPage>();
			Assert.IsType<TestPageViewModel>(requiredService.ViewModel);
			Assert.Same(requiredService.ViewModel, requiredService.DataContext);
		});
	}

	/// <summary>
	/// 页面生命周期应完整转发给 ViewModel。
	/// </summary>
	[Fact]
	public void AxamlPageForwardsNavigationLifecycle()
	{
		GuiParityAndFacadeTests.RunOnUiThread(delegate
		{
			TestPageViewModel testPageViewModel = new TestPageViewModel();
			TestPage testPage = new TestPage(testPageViewModel);
			testPage.OnPageShown();
			testPage.OnPageLeave();
			testPage.OnPageHidden();
			testPage.DisposePage();
			testPage.DisposePage();
			Assert.Equal(1, testPageViewModel.ShownCount);
			Assert.Equal(1, testPageViewModel.LeaveCount);
			Assert.Equal(1, testPageViewModel.HiddenCount);
			Assert.Equal(1, testPageViewModel.DisposeCount);
		});
	}
}
