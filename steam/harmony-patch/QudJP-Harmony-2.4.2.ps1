#requires -Version 5.1

param(
    [ValidateSet('Install', 'Restore')]
    [string]$Operation,
    [string]$TargetDll
)

$ErrorActionPreference = 'Stop'
$SupportedGameSha256 = '0de0118c8f1d4408de389ca33b46d2ff7778f3a8541b430cae729ec913d899c7'
$PayloadSha256 = '77e6901ecc606aec66c2a972782a3779e4f50c037d2d165eb7ececdd4d8f794d'
$BackupName = '0Harmony.dll.qudjp-backup-before-2.4.2'
$GameDllSuffix = 'Caves of Qud\CoQ_Data\Managed\0Harmony.dll'
$MutationLockName = '.qudjp-harmony-2.4.2.lock'

function Get-FileSha256 {
    param([string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Assert-FileHash {
    param(
        [string]$Path,
        [string]$ExpectedHash,
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Description が見つかりません: $Path"
    }
    $actualHash = Get-FileSha256 -Path $Path
    if ($actualHash -ne $ExpectedHash) {
        throw "$Description の SHA-256 が確認済みハッシュと一致しません。処理を中止します。`nパス: $Path`nSHA-256: $actualHash"
    }
}

function Resolve-ValidatedTargetDll {
    param([string]$CandidatePath)

    if ([string]::IsNullOrWhiteSpace($CandidatePath)) {
        throw '0Harmony.dll のパスが指定されていません。'
    }
    if (-not (Test-Path -LiteralPath $CandidatePath -PathType Leaf)) {
        throw "指定された 0Harmony.dll が見つかりません: $CandidatePath"
    }
    $resolvedPath = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $CandidatePath).ProviderPath)
    if (-not $resolvedPath.EndsWith($GameDllSuffix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "対象は Caves of Qud\CoQ_Data\Managed\0Harmony.dll に限られます: $resolvedPath"
    }
    return $resolvedPath
}

function Get-SteamInstallPaths {
    $paths = @()
    foreach ($registryPath in @(
        'HKCU:\SOFTWARE\Valve\Steam',
        'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam',
        'HKLM:\SOFTWARE\Valve\Steam'
    )) {
        try {
            $installPath = (Get-ItemProperty -LiteralPath $registryPath -Name InstallPath -ErrorAction Stop).InstallPath
            if ($installPath) {
                $paths += $installPath
            }
        }
        catch {
            # Steam がこのレジストリ位置にない場合は、ほかの候補を続けて確認する。
        }
    }

    if (${env:ProgramFiles(x86)}) {
        $paths += (Join-Path ${env:ProgramFiles(x86)} 'Steam')
    }
    if ($env:ProgramFiles) {
        $paths += (Join-Path $env:ProgramFiles 'Steam')
    }
    return @($paths | Where-Object { $_ } | Select-Object -Unique)
}

function Get-SteamLibraryRoots {
    $roots = @()
    foreach ($steamPath in @(Get-SteamInstallPaths)) {
        $roots += $steamPath
        $vdfPath = Join-Path $steamPath 'steamapps\libraryfolders.vdf'
        if (-not (Test-Path -LiteralPath $vdfPath -PathType Leaf)) {
            continue
        }
        try {
            $vdfLines = Get-Content -LiteralPath $vdfPath -ErrorAction Stop
        }
        catch {
            # 読み取れないライブラリ設定だけを除外し、ほかの候補を続けて確認する。
            continue
        }
        foreach ($line in $vdfLines) {
            $libraryPath = $null
            if ($line -match '"path"\s+"(?<path>[^"]+)"') {
                $libraryPath = $Matches['path']
            }
            elseif ($line -match '^\s*"\d+"\s+"(?<path>[^"]+)"') {
                $libraryPath = $Matches['path']
            }
            if ($libraryPath) {
                $roots += $libraryPath.Replace('\\', '\')
            }
        }
    }

    foreach ($drive in @(Get-PSDrive -PSProvider FileSystem -ErrorAction SilentlyContinue)) {
        if ($drive.Root) {
            $roots += (Join-Path $drive.Root 'SteamLibrary')
        }
    }
    return @($roots | Where-Object { $_ } | Select-Object -Unique)
}

function Find-SteamTargetDll {
    foreach ($libraryRoot in @(Get-SteamLibraryRoots)) {
        $candidate = Join-Path $libraryRoot 'steamapps\common\Caves of Qud\CoQ_Data\Managed\0Harmony.dll'
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return Resolve-ValidatedTargetDll -CandidatePath $candidate
        }
    }
    return $null
}

function Select-TargetDll {
    Add-Type -AssemblyName PresentationFramework
    $dialog = New-Object Microsoft.Win32.OpenFileDialog
    $dialog.Title = 'Caves of Qud の CoQ_Data\Managed\0Harmony.dll を選択してください'
    $dialog.Filter = '0Harmony.dll|0Harmony.dll'
    $dialog.CheckFileExists = $true
    $dialog.Multiselect = $false
    if ($dialog.ShowDialog() -ne $true) {
        throw '0Harmony.dll が選択されなかったため、処理を中止しました。'
    }
    return Resolve-ValidatedTargetDll -CandidatePath $dialog.FileName
}

function Resolve-TargetDll {
    param([string]$RequestedTarget)

    if (-not [string]::IsNullOrWhiteSpace($RequestedTarget)) {
        return Resolve-ValidatedTargetDll -CandidatePath $RequestedTarget
    }
    $discoveredTarget = Find-SteamTargetDll
    if ($discoveredTarget) {
        Write-Host "Caves of Qud を検出しました: $discoveredTarget"
        return $discoveredTarget
    }
    Write-Host 'Steam ライブラリから Caves of Qud を検出できませんでした。対象 DLL を手動で選択してください。'
    return Select-TargetDll
}

function Assert-CoQNotRunning {
    if (@(Get-Process -Name 'CoQ' -ErrorAction SilentlyContinue).Count -gt 0) {
        throw 'Caves of Qud (CoQ.exe) が起動中です。ゲームを完全に終了してから、もう一度実行してください。'
    }
}

function Test-DirectoryWritable {
    param([string]$Directory)

    $probePath = Join-Path $Directory ('.qudjp-write-test-{0}.tmp' -f [guid]::NewGuid().ToString('N'))
    try {
        $stream = [IO.File]::Open($probePath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        $stream.Dispose()
        return $true
    }
    catch {
        return $false
    }
    finally {
        if (Test-Path -LiteralPath $probePath) {
            Remove-Item -LiteralPath $probePath -Force -ErrorAction SilentlyContinue
        }
    }
}

function Enter-MutationLock {
    param([string]$TargetPath)

    $targetDirectory = Split-Path -Parent $TargetPath
    $lockPath = Join-Path $targetDirectory $MutationLockName
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    while ($true) {
        if ($stopwatch.Elapsed.TotalSeconds -ge 30) {
            throw '別の QudJP Harmony 更新処理が実行中です。完了後にもう一度実行してください。'
        }
        try {
            return [IO.File]::Open(
                $lockPath,
                [IO.FileMode]::OpenOrCreate,
                [IO.FileAccess]::ReadWrite,
                [IO.FileShare]::None
            )
        }
        catch [IO.IOException] {
            Start-Sleep -Milliseconds 200
        }
    }
}

function Exit-MutationLock {
    param([IO.FileStream]$LockStream)

    $LockStream.Dispose()
}

function Request-LiteralConfirmation {
    param(
        [string]$ExpectedLiteral,
        [string]$Message
    )

    Write-Host $Message
    $answer = Read-Host "続行する場合は $ExpectedLiteral と半角大文字で入力してください"
    if ($answer -cne $ExpectedLiteral) {
        throw '確認文字列が一致しなかったため、ファイルは変更していません。'
    }
}

function Copy-VerifiedOriginalBackup {
    param(
        [string]$TargetPath,
        [string]$BackupPath
    )

    $backupDirectory = Split-Path -Parent $BackupPath
    $temporaryPath = Join-Path $backupDirectory ('.0Harmony.dll.qudjp-backup-{0}.tmp' -f [guid]::NewGuid().ToString('N'))
    $sourceStream = $null
    $temporaryStream = $null
    try {
        $sourceStream = [IO.File]::Open(
            $TargetPath,
            [IO.FileMode]::Open,
            [IO.FileAccess]::Read,
            [IO.FileShare]::None
        )
        $temporaryStream = [IO.File]::Open(
            $temporaryPath,
            [IO.FileMode]::CreateNew,
            [IO.FileAccess]::Write,
            [IO.FileShare]::None
        )
        $sourceStream.CopyTo($temporaryStream)
        $temporaryStream.Flush($true)
        $temporaryStream.Dispose()
        $temporaryStream = $null
        $sourceStream.Dispose()
        $sourceStream = $null

        Assert-FileHash -Path $temporaryPath -ExpectedHash $SupportedGameSha256 -Description '一時バックアップ'
        try {
            [IO.File]::Move($temporaryPath, $BackupPath)
        }
        catch [IO.IOException] {
            if (-not (Test-Path -LiteralPath $BackupPath -PathType Leaf)) {
                throw
            }
            Assert-FileHash -Path $BackupPath -ExpectedHash $SupportedGameSha256 -Description '既存の QudJP バックアップ'
            Write-Host "確認済みバックアップを再利用します（上書きしません）: $BackupPath"
            return
        }

        Assert-FileHash -Path $BackupPath -ExpectedHash $SupportedGameSha256 -Description '作成した QudJP バックアップ'
        Write-Host "ゲーム同梱 Harmony をバックアップしました: $BackupPath"
    }
    finally {
        if ($null -ne $temporaryStream) {
            $temporaryStream.Dispose()
        }
        if ($null -ne $sourceStream) {
            $sourceStream.Dispose()
        }
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
        }
    }
}

function Replace-WithVerifiedFile {
    param(
        [string]$SourcePath,
        [string]$TargetPath,
        [string]$ExpectedHash
    )

    $targetDirectory = Split-Path -Parent $TargetPath
    $temporaryPath = Join-Path $targetDirectory ('.0Harmony.dll.qudjp-{0}.tmp' -f [guid]::NewGuid().ToString('N'))
    try {
        Copy-Item -LiteralPath $SourcePath -Destination $temporaryPath
        Assert-FileHash -Path $temporaryPath -ExpectedHash $ExpectedHash -Description '一時コピー'
        Move-Item -LiteralPath $temporaryPath -Destination $TargetPath -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
        }
    }
}

function Restore-VerifiedBackupOnFailure {
    param(
        [string]$BackupPath,
        [string]$TargetPath
    )

    Assert-FileHash -Path $BackupPath -ExpectedHash $SupportedGameSha256 -Description 'ロールバック用バックアップ'
    Replace-WithVerifiedFile -SourcePath $BackupPath -TargetPath $TargetPath -ExpectedHash $SupportedGameSha256
    Assert-FileHash -Path $TargetPath -ExpectedHash $SupportedGameSha256 -Description 'ロールバック後のゲーム Harmony'
}

function Install-Harmony {
    param(
        [string]$ResolvedTarget,
        [string]$PayloadPath
    )

    $backupPath = Join-Path (Split-Path -Parent $ResolvedTarget) $BackupName
    $currentHash = Get-FileSha256 -Path $ResolvedTarget
    if ($currentHash -eq $PayloadSha256) {
        $stateLock = Enter-MutationLock -TargetPath $ResolvedTarget
        try {
            Assert-FileHash -Path $ResolvedTarget -ExpectedHash $PayloadSha256 -Description '現在の Harmony 2.4.2'
            try {
                Assert-FileHash -Path $backupPath -ExpectedHash $SupportedGameSha256 -Description '復元用 QudJP バックアップ'
            }
            catch {
                throw "Harmony 2.4.2 は導入されていますが、確認済みの復元用バックアップがありません。Steam の「インストール済みファイルの整合性を確認」でゲーム同梱 Harmony に戻してから、この更新パッチをもう一度実行してください。`n詳細: $($_.Exception.Message)"
            }
            Write-Host 'Harmony 2.4.2 はすでに導入済みで、復元用バックアップも確認できました。変更は不要です。'
            return
        }
        finally {
            Exit-MutationLock -LockStream $stateLock
        }
    }
    if ($currentHash -ne $SupportedGameSha256) {
        throw "ゲーム本体の 0Harmony.dll が Caves of Qud 1.0.5 の確認済みファイルではありません。処理を中止します。`nSHA-256: $currentHash"
    }

    Request-LiteralConfirmation -ExpectedLiteral 'INSTALL' -Message "Harmony 2.4.2 に更新します。`n対象: $ResolvedTarget`nバックアップ: $backupPath"
    $mutationLock = Enter-MutationLock -TargetPath $ResolvedTarget
    try {
        Assert-FileHash -Path $ResolvedTarget -ExpectedHash $SupportedGameSha256 -Description '更新直前のゲーム Harmony'
        Assert-CoQNotRunning
        Copy-VerifiedOriginalBackup -TargetPath $ResolvedTarget -BackupPath $backupPath
        try {
            Replace-WithVerifiedFile -SourcePath $PayloadPath -TargetPath $ResolvedTarget -ExpectedHash $PayloadSha256
            Assert-FileHash -Path $ResolvedTarget -ExpectedHash $PayloadSha256 -Description '更新後の Harmony 2.4.2'
        }
        catch {
            $installFailure = $_.Exception.Message
            try {
                Restore-VerifiedBackupOnFailure -BackupPath $backupPath -TargetPath $ResolvedTarget
            }
            catch {
                throw "Harmony の更新と自動復元の両方に失敗しました。ゲームを起動せず、QudJP のサポートへ連絡してください。`n更新エラー: $installFailure`n復元エラー: $($_.Exception.Message)"
            }
            throw "Harmony の更新に失敗したため、確認済みバックアップへ復元しました。`n原因: $installFailure"
        }
    }
    finally {
        Exit-MutationLock -LockStream $mutationLock
    }
    Write-Host 'Harmony 2.4.2 への更新が完了しました。バックアップは削除せず保管してください。'
}

function Restore-GameHarmony {
    param(
        [string]$ResolvedTarget,
        [string]$PayloadPath
    )

    $backupPath = Join-Path (Split-Path -Parent $ResolvedTarget) $BackupName
    Assert-FileHash -Path $ResolvedTarget -ExpectedHash $PayloadSha256 -Description '現在の Harmony 2.4.2'
    Assert-FileHash -Path $backupPath -ExpectedHash $SupportedGameSha256 -Description '復元用 QudJP バックアップ'
    Request-LiteralConfirmation -ExpectedLiteral 'RESTORE' -Message "ゲーム同梱 Harmony に戻します。`n対象: $ResolvedTarget`n復元元: $backupPath"
    $mutationLock = Enter-MutationLock -TargetPath $ResolvedTarget
    try {
        Assert-FileHash -Path $ResolvedTarget -ExpectedHash $PayloadSha256 -Description '復元直前の Harmony 2.4.2'
        Assert-FileHash -Path $backupPath -ExpectedHash $SupportedGameSha256 -Description '復元直前の QudJP バックアップ'
        Assert-CoQNotRunning
        try {
            Replace-WithVerifiedFile -SourcePath $backupPath -TargetPath $ResolvedTarget -ExpectedHash $SupportedGameSha256
            Assert-FileHash -Path $ResolvedTarget -ExpectedHash $SupportedGameSha256 -Description '復元後のゲーム Harmony'
        }
        catch {
            $restoreFailure = $_.Exception.Message
            try {
                Replace-WithVerifiedFile -SourcePath $PayloadPath -TargetPath $ResolvedTarget -ExpectedHash $PayloadSha256
                Assert-FileHash -Path $ResolvedTarget -ExpectedHash $PayloadSha256 -Description '復元失敗後の Harmony 2.4.2'
            }
            catch {
                throw "ゲーム同梱 Harmony の復元と Harmony 2.4.2 へのロールバックに失敗しました。ゲームを起動せず、QudJP のサポートへ連絡してください。`n復元エラー: $restoreFailure`nロールバックエラー: $($_.Exception.Message)"
            }
            throw "ゲーム同梱 Harmony の復元に失敗したため、Harmony 2.4.2 へ戻しました。`n原因: $restoreFailure"
        }
    }
    finally {
        Exit-MutationLock -LockStream $mutationLock
    }
    Write-Host 'ゲーム同梱 Harmony の復元が完了しました。バックアップは削除せず保管しています。'
}

function Invoke-Updater {
    if ($Operation -notin @('Install', 'Restore')) {
        throw '操作は Install または Restore を明示してください。'
    }

    $payloadPath = Join-Path $PSScriptRoot 'payload\net48\0Harmony.dll'
    Assert-FileHash -Path $payloadPath -ExpectedHash $PayloadSha256 -Description '同梱 Harmony 2.4.2 payload'
    $resolvedTarget = Resolve-TargetDll -RequestedTarget $TargetDll
    Assert-CoQNotRunning

    $targetDirectory = Split-Path -Parent $resolvedTarget
    if (-not (Test-DirectoryWritable -Directory $targetDirectory)) {
        if ($Operation -eq 'Install') {
            $launcherName = 'Install Harmony 2.4.2.cmd'
        }
        else {
            $launcherName = 'Restore Game Harmony.cmd'
        }
        throw "ゲームフォルダーへ書き込めません。この画面を閉じ、エクスプローラーで $launcherName を右クリックして「管理者として実行」してください。`n対象: $targetDirectory"
    }

    if ($Operation -eq 'Install') {
        Install-Harmony -ResolvedTarget $resolvedTarget -PayloadPath $payloadPath
    }
    else {
        Restore-GameHarmony -ResolvedTarget $resolvedTarget -PayloadPath $payloadPath
    }
}

try {
    Invoke-Updater
    exit 0
}
catch {
    [Console]::Error.WriteLine("エラー: $($_.Exception.Message)")
    exit 1
}
