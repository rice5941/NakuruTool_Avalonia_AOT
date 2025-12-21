using Avalonia.Collections;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using System;

namespace NakuruTool_Avalonia_AOT.Features.Licenses;

public interface ILicensesViewModel : IDisposable
{
    IAvaloniaReadOnlyList<LicenseItem> Licenses { get; }
}

public class LicensesViewModel : ViewModelBase, ILicensesViewModel
{
    public IAvaloniaReadOnlyList<LicenseItem> Licenses { get; }

    public LicensesViewModel()
    {
        var licenses = new AvaloniaList<LicenseItem>
        {
            new LicenseItem
            {
                PackageName = "Avalonia",
                Version = "11.3.10",
                LicenseType = "MIT",
                Url = "https://github.com/AvaloniaUI/Avalonia",
                Copyright = "Copyright (c) The Avalonia Project"
            },
            new LicenseItem
            {
                PackageName = "CommunityToolkit.Mvvm",
                Version = "8.4.0",
                LicenseType = "MIT",
                Url = "https://github.com/CommunityToolkit/dotnet",
                Copyright = "Copyright (c) .NET Foundation and Contributors"
            },
            new LicenseItem
            {
                PackageName = "R3",
                Version = "1.3.0",
                LicenseType = "MIT",
                Url = "https://github.com/Cysharp/R3",
                Copyright = "Copyright (c) 2024 Cysharp, Inc."
            },
            new LicenseItem
            {
                PackageName = "Semi.Avalonia",
                Version = "11.3.7.1",
                LicenseType = "MIT",
                Url = "https://github.com/irihitech/Semi.Avalonia",
                Copyright = "Copyright (c) 2024 Irihi"
            },
            new LicenseItem
            {
                PackageName = "Pure.DI",
                Version = "2.2.15",
                LicenseType = "MIT",
                Url = "https://github.com/DevTeam/Pure.DI",
                Copyright = "Copyright (c) 2023 Team DevTeam"
            },
            new LicenseItem
            {
                PackageName = "Material.Icons.Avalonia",
                Version = "2.4.1",
                LicenseType = "MIT",
                Url = "https://github.com/AvaloniaUtils/Material.Icons.Avalonia",
                Copyright = "Copyright (c) 2021 AvaloniaUtils"
            },
            new LicenseItem
            {
                PackageName = "OsuParsers",
                Version = "1.8.3.8",
                LicenseType = "MIT",
                Url = "https://github.com/krrcream/OsuParsers",
                Copyright = "Copyright (c) krrcream"
            },
            new LicenseItem
            {
                PackageName = "ZLinq",
                Version = "1.5.4",
                LicenseType = "MIT",
                Url = "https://github.com/Cysharp/ZLinq",
                Copyright = "Copyright (c) 2024 Cysharp, Inc."
            },
            new LicenseItem
            {
                PackageName = "HotAvalonia",
                Version = "3.0.2",
                LicenseType = "MIT",
                Url = "https://github.com/Kir-Antipov/HotAvalonia",
                Copyright = "Copyright (c) 2023 Kir_Antipov"
            }
        };

        Licenses = licenses;
    }
}
