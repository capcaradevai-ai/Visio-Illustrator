$ErrorActionPreference = "Stop"

$output = Join-Path $PSScriptRoot "..\plugins\visio-scientific-illustrator\assets\visio-scientific-masters.vssx"
$output = [IO.Path]::GetFullPath($output)
$outputDirectory = Split-Path -Parent $output
[IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$staleBuilds = Get-ChildItem -LiteralPath $outputDirectory -Filter "visio-scientific-masters.build.*.vssx" -File
foreach ($staleBuild in $staleBuilds) {
    if ($staleBuild.DirectoryName -ne $outputDirectory) {
        throw "Refusing to remove a stale stencil outside the asset directory."
    }
    Remove-Item -LiteralPath $staleBuild.FullName -Force
}
$temporaryOutput = Join-Path $outputDirectory ("visio-scientific-masters.build.{0}.vssx" -f [Guid]::NewGuid().ToString("N"))

$application = New-Object -ComObject Visio.InvisibleApp
Write-Host "Hidden Visio stencil builder started."
$stencil = $null
$containerStencil = $null
$scratch = $null
try {
    $documents = $application.Documents
    try {
        $stencil = $documents.Add("vss")
        Write-Host "Empty stencil created."
        $scratch = $documents.Add("")
    }
    finally {
        [void][Runtime.InteropServices.Marshal]::ReleaseComObject($documents)
    }

    $masters = $stencil.Masters
    try {
        function Add-RectangleMaster([string]$name, [double]$rounding) {
            Write-Host "Creating $name"
            $page = $scratch.Pages.Item(1)
            try {
                $shape = $page.DrawRectangle(0, 0, 2, 1)
                try {
                    if ($rounding -gt 0) {
                        $cell = $shape.CellsU("Rounding")
                        try { $cell.ResultIU = $rounding }
                        finally { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($cell) }
                    }
                    $master = $masters.Drop($shape, 0, 0)
                    try {
                        $master.Name = $name
                        $master.NameU = $name
                    }
                    finally {
                        [void][Runtime.InteropServices.Marshal]::ReleaseComObject($master)
                    }
                }
                finally {
                    $shape.Delete()
                    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($shape)
                }
            }
            finally {
                [void][Runtime.InteropServices.Marshal]::ReleaseComObject($page)
            }
        }

        function Add-EllipseMaster([string]$name) {
            Write-Host "Creating $name"
            $page = $scratch.Pages.Item(1)
            try {
                $shape = $page.DrawOval(0, 0, 2, 1)
                try {
                    $master = $masters.Drop($shape, 0, 0)
                    try {
                        $master.Name = $name
                        $master.NameU = $name
                    }
                    finally {
                        [void][Runtime.InteropServices.Marshal]::ReleaseComObject($master)
                    }
                }
                finally {
                    $shape.Delete()
                    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($shape)
                }
            }
            finally {
                [void][Runtime.InteropServices.Marshal]::ReleaseComObject($page)
            }
        }

        function Add-PolygonMaster([string]$name, [double[]]$points) {
            Write-Host "Creating $name"
            $page = $scratch.Pages.Item(1)
            try {
                $shape = $page.DrawPolyline([ref]$points, 0)
                try {
                    $master = $masters.Drop($shape, 0, 0)
                    try {
                        $master.Name = $name
                        $master.NameU = $name
                    }
                    finally {
                        [void][Runtime.InteropServices.Marshal]::ReleaseComObject($master)
                    }
                }
                finally {
                    $shape.Delete()
                    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($shape)
                }
            }
            finally {
                [void][Runtime.InteropServices.Marshal]::ReleaseComObject($page)
            }
        }

        function Set-CellResult([object]$shape, [string]$cellName, [double]$value) {
            $cell = $shape.CellsU($cellName)
            try { $cell.ResultIU = $value }
            finally { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($cell) }
        }

        function Add-GroupedMaster([string]$name, [scriptblock]$builder) {
            Write-Host "Creating $name"
            $page = $scratch.Pages.Item(1)
            $selection = $null
            $group = $null
            $builtShapes = @()
            try {
                $builtShapes = @(& $builder $page)
                if ($builtShapes.Count -lt 2) { throw "$name must contain at least two editable shapes." }
                $selection = $page.CreateSelection(0, 0, $null)
                foreach ($builtShape in $builtShapes) { $selection.Select($builtShape, 2) }
                $group = $selection.Group()
                $master = $masters.Drop($group, 0, 0)
                try {
                    $master.Name = $name
                    $master.NameU = $name
                }
                finally { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($master) }
            }
            finally {
                if ($null -ne $group) {
                    $group.Delete()
                    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($group)
                }
                if ($null -ne $selection) { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($selection) }
                foreach ($builtShape in $builtShapes) {
                    if ($null -ne $builtShape) { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($builtShape) }
                }
                [void][Runtime.InteropServices.Marshal]::ReleaseComObject($page)
            }
        }

        Add-RectangleMaster "Codex Rounded Rectangle" 0.12
        Add-PolygonMaster "Codex Diamond" ([double[]](0, 0.5, 1, 1, 2, 0.5, 1, 0, 0, 0.5))
        Add-GroupedMaster "Codex Cylinder" {
            param($page)
            @(
                $page.DrawRectangle(0.15, 0.15, 1.85, 0.85),
                $page.DrawOval(0.15, 0.7, 1.85, 1.0),
                $page.DrawOval(0.15, 0.0, 1.85, 0.3)
            )
        }
        Add-PolygonMaster "Codex Hexagon" ([double[]](0, 0.5, 0.4, 1, 1.6, 1, 2, 0.5, 1.6, 0, 0.4, 0, 0, 0.5))
        Add-PolygonMaster "Codex Triangle" ([double[]](0, 0, 2, 0, 1, 1, 0, 0))
        Add-PolygonMaster "Codex Parallelogram" ([double[]](0.4, 0, 2, 0, 1.6, 1, 0, 1, 0.4, 0))
        Add-GroupedMaster "Codex Cloud" {
            param($page)
            @(
                $page.DrawOval(0.00, 0.10, 0.75, 0.70),
                $page.DrawOval(0.35, 0.35, 1.10, 1.00),
                $page.DrawOval(0.75, 0.25, 1.55, 1.00),
                $page.DrawOval(1.20, 0.15, 2.00, 0.80),
                $page.DrawOval(0.35, 0.00, 1.15, 0.55),
                $page.DrawOval(0.90, 0.00, 1.65, 0.55)
            )
        }
        Add-PolygonMaster "Codex Tensor Face" ([double[]](0, 0.2, 0.4, 1, 2, 0.8, 1.6, 0, 0, 0.2))
        Add-GroupedMaster "Codex Video Frame" {
            param($page)
            @(
                $page.DrawRectangle(0.25, 0.20, 2.00, 1.15),
                $page.DrawRectangle(0.12, 0.10, 1.87, 1.05),
                $page.DrawRectangle(0.00, 0.00, 1.75, 0.95)
            )
        }
        Add-GroupedMaster "Codex Latent Vector" {
            param($page)
            @(
                $page.DrawRectangle(0.0, 0.0, 0.8, 2.0),
                $page.DrawOval(0.28, 1.55, 0.52, 1.79),
                $page.DrawOval(0.28, 1.10, 0.52, 1.34),
                $page.DrawOval(0.28, 0.65, 0.52, 0.89),
                $page.DrawOval(0.28, 0.20, 0.52, 0.44)
            )
        }
        Add-GroupedMaster "Codex Snowflake" {
            param($page)
            $items = @()
            for ($axis = 0; $axis -lt 6; $axis++) {
                $angle = [Math]::PI * $axis / 3
                $items += $page.DrawLine(1, 1, 1 + [Math]::Cos($angle), 1 + [Math]::Sin($angle))
            }
            $items
        }
        Add-GroupedMaster "Codex Router" {
            param($page)
            $body = $page.DrawRectangle(0.0, 0.0, 2.0, 0.65)
            Set-CellResult $body "Rounding" 0.12
            @(
                $body,
                $page.DrawLine(0.45, 0.55, 0.25, 1.3),
                $page.DrawLine(1.55, 0.55, 1.75, 1.3),
                $page.DrawOval(0.35, 0.17, 0.47, 0.29),
                $page.DrawOval(0.55, 0.17, 0.67, 0.29),
                $page.DrawOval(0.75, 0.17, 0.87, 0.29)
            )
        }
        Add-GroupedMaster "Codex Antenna" {
            param($page)
            @(
                $page.DrawRectangle(0.7, 0.0, 1.3, 0.22),
                $page.DrawLine(1.0, 0.2, 1.0, 1.5),
                $page.DrawLine(1.0, 1.5, 0.55, 2.0),
                $page.DrawLine(1.0, 1.5, 1.45, 2.0)
            )
        }
        Add-GroupedMaster "Codex Stage Panel" {
            param($page)
            $panel = $page.DrawRectangle(0.0, 0.0, 3.0, 2.0)
            Set-CellResult $panel "Rounding" 0.1
            @($panel, $page.DrawRectangle(0.08, 1.55, 2.92, 1.92))
        }

        $containerPath = $application.GetBuiltInStencilFile(2, 1)
        Write-Host "Opening built-in container stencil."
        $documents = $application.Documents
        try {
            $containerStencil = $documents.OpenEx($containerPath, 2 -bor 64 -bor 8 -bor 128)
        }
        finally {
            [void][Runtime.InteropServices.Marshal]::ReleaseComObject($documents)
        }
        $containerMasters = $containerStencil.Masters
        try {
            $plain = $containerMasters.ItemU("Plain")
            try {
                $container = $masters.Drop($plain, 0, 0)
                try {
                    $container.Name = "Codex Container"
                    $container.NameU = "Codex Container"
                }
                finally {
                    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($container)
                }
            }
            finally {
                [void][Runtime.InteropServices.Marshal]::ReleaseComObject($plain)
            }
        }
        finally {
            [void][Runtime.InteropServices.Marshal]::ReleaseComObject($containerMasters)
        }
    }
    finally {
        [void][Runtime.InteropServices.Marshal]::ReleaseComObject($masters)
    }

    [void]$stencil.SaveAs($temporaryOutput)
    Write-Host "Stencil saved."
}
finally {
    if ($null -ne $scratch) {
        $scratch.Saved = $true
        $scratch.Close()
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($scratch)
    }
    if ($null -ne $containerStencil) {
        $containerStencil.Close()
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($containerStencil)
    }
    if ($null -ne $stencil) {
        $stencil.Saved = $true
        $stencil.Close()
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($stencil)
    }
    $application.Quit()
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($application)
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
}

Move-Item -LiteralPath $temporaryOutput -Destination $output -Force

Get-Item -LiteralPath $output
