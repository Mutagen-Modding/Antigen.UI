using Antigen.Modules;
using Antigen.Views;
using Autofac;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Antigen;

public sealed class App : Application
{
    public static IContainer? Container { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            Container = SetupServices(window);

            var startup = Container.Resolve<AppStartup>();
            startup.Start();

            desktop.MainWindow = window;
            desktop.Exit += (_, _) => startup.Shutdown();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IContainer SetupServices(MainWindow window)
    {
        var builder = new ContainerBuilder();

        builder.RegisterInstance(window)
            .As<IMainWindow>()
            .As<Window>();

        builder.RegisterModule<MainModule>();

        return builder.Build();
    }
}
