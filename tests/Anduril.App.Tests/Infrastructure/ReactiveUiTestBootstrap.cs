using System.Runtime.CompilerServices;
using ReactiveUI.Builder;

namespace Anduril.App.Tests.Infrastructure;

internal static class ReactiveUiTestBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        RxAppBuilder.CreateReactiveUIBuilder()
            .BuildApp();
    }
}
