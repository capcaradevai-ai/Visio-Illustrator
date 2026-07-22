$ErrorActionPreference = "Stop"

$output = Join-Path $PSScriptRoot "..\plugins\visio-scientific-illustrator\assets\visio-scientific-masters.vssx"
$output = [IO.Path]::GetFullPath($output)
$outputDirectory = Split-Path -Parent $output
[IO.Directory]::CreateDirectory($outputDirectory) | Out-Null

$application = New-Object -ComObject Visio.Application
$application.Visible = $true
Write-Host "Visio started."
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

        Add-RectangleMaster "Codex Rounded Rectangle" 0.12
        Add-PolygonMaster "Codex Diamond" ([double[]](0, 0.5, 1, 1, 2, 0.5, 1, 0, 0, 0.5))
        Add-EllipseMaster "Codex Cylinder"
        Add-PolygonMaster "Codex Hexagon" ([double[]](0, 0.5, 0.4, 1, 1.6, 1, 2, 0.5, 1.6, 0, 0.4, 0, 0, 0.5))
        Add-PolygonMaster "Codex Triangle" ([double[]](0, 0, 2, 0, 1, 1, 0, 0))
        Add-PolygonMaster "Codex Parallelogram" ([double[]](0.4, 0, 2, 0, 1.6, 1, 0, 1, 0.4, 0))
        Add-EllipseMaster "Codex Cloud"

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

    if (Test-Path -LiteralPath $output) {
        throw "Stencil output already exists: $output"
    }
    [void]$stencil.SaveAs($output)
    Write-Host "Stencil saved."
}
finally {
    if ($null -ne $containerStencil) {
        $containerStencil.Close()
        [void][Runtime.InteropServices.Marshal]::ReleaseComObject($containerStencil)
    }
    if ($null -ne $stencil) {
        $stencil.Close()
        [void][Runtime.InteropServices.Marshal]::ReleaseComObject($stencil)
    }
    if ($null -ne $scratch) {
        $scratch.Saved = $true
        $scratch.Close()
        [void][Runtime.InteropServices.Marshal]::ReleaseComObject($scratch)
    }
    $application.Quit()
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($application)
}

Get-Item -LiteralPath $output
