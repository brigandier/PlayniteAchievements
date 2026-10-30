param(
    [switch]$SemTestes,
    [switch]$SemAtualizar
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = $root
if (-not (Test-Path (Join-Path $repo 'source\PlayniteAchievements.csproj'))) {
    $repo = Join-Path $root 'PlayniteAchievements'
}

try {
    if (-not (Test-Path (Join-Path $repo 'source\PlayniteAchievements.csproj'))) {
        $git = Get-Command git.exe -ErrorAction SilentlyContinue
        if (-not $git) { throw 'Instale o Git for Windows antes de executar: https://git-scm.com/downloads/win' }
        Write-Host '[1/7] Clonando a fork...' -ForegroundColor Cyan
        & $git.Source clone 'https://github.com/brigandier/PlayniteAchievements.git' $repo
        if ($LASTEXITCODE -ne 0) { throw 'Falha ao clonar o repositorio.' }
    }

    if (-not $SemAtualizar -and (Test-Path (Join-Path $repo '.git'))) {
        $git = Get-Command git.exe -ErrorAction SilentlyContinue
        if ($git) {
            Write-Host '[2/7] Atualizando main sem descartar alteracoes locais...' -ForegroundColor Cyan
            & $git.Source -C $repo pull --ff-only origin main
            if ($LASTEXITCODE -ne 0) {
                throw 'Falha ao atualizar main. Verifique alteracoes locais e execute git pull --ff-only manualmente.'
            }
        }
    }

    $outputDir = Join-Path $repo 'artefatos'
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
    $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $transcript = Join-Path $outputDir ("compilacao_$stamp.log")
    Start-Transcript -Path $transcript -Force | Out-Null

    try {
        Write-Host "Repositorio: $repo"
        Write-Host "Registro: $transcript"

        $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
        if (-not (Test-Path $vswhere)) {
            throw 'Visual Studio 2022 ou Build Tools nao encontrado. Instale a carga .NET Desktop Build Tools, o Targeting Pack .NET Framework 4.6.2 e o Windows 10 SDK (10.0.19041).'
        }

        $msbuild = @(& $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Where-Object { Test-Path $_ } | Select-Object -First 1)
        if ($msbuild.Count -eq 0) { throw 'MSBuild nao encontrado. Instale a carga .NET Desktop Build Tools no Visual Studio Installer.' }
        $msbuild = $msbuild[0]
        Write-Host "MSBuild: $msbuild"

        $nuget = Join-Path $repo 'ferramentas\nuget.exe'
        if (-not (Test-Path $nuget)) {
            $installedNuget = Get-Command nuget.exe -ErrorAction SilentlyContinue
            if ($installedNuget) {
                $nuget = $installedNuget.Source
            } else {
                Write-Host '[3/7] Baixando NuGet da distribuicao oficial...' -ForegroundColor Cyan
                New-Item -ItemType Directory -Force -Path (Split-Path -Parent $nuget) | Out-Null
                [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
                Invoke-WebRequest -Uri 'https://dist.nuget.org/win-x86-commandline/latest/nuget.exe' -OutFile $nuget -UseBasicParsing
            }
        }

        $solution = Join-Path $repo 'source\PlayniteAchievements.sln'
        $project = Join-Path $repo 'source\PlayniteAchievements.csproj'
        if (-not (Test-Path $solution) -or -not (Test-Path $project)) { throw 'Solucao/projeto do Playnite Achievements nao encontrado.' }

        Write-Host '[4/7] Restaurando dependencias NuGet...' -ForegroundColor Cyan
        & $nuget restore $solution -NonInteractive -Verbosity normal
        if ($LASTEXITCODE -ne 0) { throw 'Falha no NuGet restore. Confira o log.' }

        Write-Host '[5/7] Compilando extensao Release...' -ForegroundColor Cyan
        $binlog = Join-Path $outputDir ("msbuild_$stamp.binlog")
        & $msbuild $project '/t:Rebuild' '/p:Configuration=Release' '/p:Platform=AnyCPU' '/m:1' '/v:minimal' "/bl:$binlog"
        if ($LASTEXITCODE -ne 0) { throw 'Compilacao falhou. Envie o log .log ou o .binlog.' }

        $release = Join-Path $repo 'source\bin\Release'
        $assembly = Join-Path $release 'PlayniteAchievements.dll'
        if (-not (Test-Path $assembly)) { throw 'DLL nao encontrada apos a compilacao.' }

        if (-not $SemTestes) {
            $dotnet = Get-Command dotnet.exe -ErrorAction SilentlyContinue
            if ($dotnet) {
                Write-Host '[6/7] Executando testes do SpartanPort...' -ForegroundColor Cyan
                $testProject = Join-Path $repo 'tests\PlayniteAchievements.Tests\PlayniteAchievements.Tests.csproj'
                & $dotnet.Source test $testProject --configuration Release --filter 'FullyQualifiedName~SpartanPort' --logger "trx;LogFileName=spartanport_$stamp.trx"
                if ($LASTEXITCODE -ne 0) {
                    throw 'Testes do SpartanPort falharam. Envie o log; o pacote ainda nao sera gerado.'
                }
            } else {
                Write-Warning 'SDK dotnet nao encontrado. Compilacao realizada, mas testes nao executados.'
            }
        }

        Write-Host '[7/7] Empacotando extensao .pext...' -ForegroundColor Cyan
        $manifest = Join-Path $repo 'source\extension.yaml'
        $icon = Join-Path $repo 'source\icon.png'
        if (-not (Test-Path $manifest) -or -not (Test-Path $icon)) {
            throw 'Manifesto extension.yaml ou icon.png ausente.'
        }

        $stage = Join-Path $outputDir "_pacote_$stamp"
        New-Item -ItemType Directory -Force -Path $stage | Out-Null
        Get-ChildItem -LiteralPath $release -Force | Where-Object { $_.Extension -notin '.pdb', '.xml' } | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination $stage -Recurse -Force
        }
        Copy-Item -LiteralPath $manifest -Destination $stage -Force
        Copy-Item -LiteralPath $icon -Destination $stage -Force
        $zip = Join-Path $outputDir "PlayniteAchievements_SpartanPort_$stamp.zip"
        $pext = [IO.Path]::ChangeExtension($zip, '.pext')
        Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
        Move-Item -LiteralPath $zip -Destination $pext -Force
        Remove-Item -LiteralPath $stage -Recurse -Force
        Write-Host "PACOTE: $pext" -ForegroundColor Green
        Write-Host "LOG: $transcript" -ForegroundColor Green
    }
    finally {
        Stop-Transcript | Out-Null
    }
}
catch {
    Write-Host "[FALHA] $($_.Exception.Message)" -ForegroundColor Red
    if ($transcript) { Write-Host "LOG: $transcript" }
    exit 1
}
exit 0
