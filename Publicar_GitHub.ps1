param()
$ErrorActionPreference = 'Stop'
$repository = 'https://github.com/markinhotec/ToccatoRomoteDesktop.git'
if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw 'Instale o Git for Windows e autentique a conta markinhotec antes de publicar.' }
$author = & git config user.name
$email = & git config user.email
if (-not $author -or -not $email) { throw 'Configure user.name e user.email no Git antes de publicar. O script não define sua identidade automaticamente.' }
$remoteHeads = & git ls-remote --heads $repository
if ($LASTEXITCODE -ne 0) { throw 'Não foi possível consultar o repositório. Confirme o acesso com o Git Credential Manager.' }
if ($remoteHeads) { throw 'O repositório já contém branches. O envio inicial foi interrompido para permitir revisão das alterações existentes. Nenhum push foi executado.' }
$work = Join-Path $env:TEMP ('TDM-Publicar-' + [Guid]::NewGuid().ToString('N'))
& git clone $repository $work
if ($LASTEXITCODE -ne 0) { throw 'Não foi possível preparar a cópia de publicação.' }
try {
    $paths = @('.github', '.gitignore', 'README.md', 'assets', 'build.ps1', 'Publicar_GitHub.ps1', 'docs', 'installer', 'src', 'tests')
    foreach ($relative in $paths) { Copy-Item (Join-Path $PSScriptRoot $relative) (Join-Path $work $relative) -Recurse -Force }
    Push-Location $work
    try {
        & git checkout -B main
        if ($LASTEXITCODE -ne 0) { throw 'Falha ao preparar a branch main.' }
        & git add -- $paths
        if ($LASTEXITCODE -ne 0) { throw 'Falha ao preparar os arquivos.' }
        & git commit -m 'feat: Toccato Desktop Manager 1.0.0'
        if ($LASTEXITCODE -ne 0) { throw 'Falha ao criar o commit.' }
        & git tag -a v1.0.0 -m 'Toccato Desktop Manager 1.0.0'
        if ($LASTEXITCODE -ne 0) { throw 'Falha ao criar a tag.' }
        & git push -u origin main
        if ($LASTEXITCODE -ne 0) { throw 'O GitHub recusou o envio. Confirme permissões de escrita do código e de workflows.' }
        & git push origin v1.0.0
        if ($LASTEXITCODE -ne 0) { throw 'A branch foi enviada, mas a tag não. Consulte o repositório antes de tentar novamente.' }
        Write-Host 'Código publicado. A compilação e os testes Windows estão na aba Actions do repositório.'
    } finally { Pop-Location }
} finally {
    # Retain the temporary checkout so a failed push can be inspected or resumed.
    Write-Host "Cópia local da publicação: $work"
}
