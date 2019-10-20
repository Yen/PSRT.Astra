$executablePath = $env:ASTRA_EXECUTABLE_PATH

Write-Host "Reading Astra assembly version"
$astraVersion = [System.Reflection.AssemblyName]::GetAssemblyName($executablePath).Version
Write-Host "Read Astra assembly version to be $($astraVersion)"

Write-Host "Creating git tag from assembly version"
$prettyVersionNumber = "$($astraVersion.Major).$($astraVersion.Minor)"
if ($astraVersion.Build -ne 0 -or $astraVersion.Revision -ne 0) {
    $prettyVersionNumber = "$($prettyVersionNumber).$($astraVersion.Build)"
}
if ($astraVersion.Revision -ne 0) {
    $prettyVersionNumber = "$($prettyVersionNumber).$($astraVersion.Revision)"
}
$gitTag = "v$($prettyVersionNumber)"
Write-Host "Assembly version converted to git tag $($gitTag)"

$secureToken = ConvertTo-SecureString $env:GITHUB_API_KEY -AsPlainText -Force

try {
    # check if a release with this tag already exists
    Invoke-RestMethod `
        -Uri "https://api.github.com/repos/Yen/PSRT.Astra/releases/tags/$($gitTag)" `
        -Authentication OAuth `
        -Token $secureToken | Out-Null
    Write-Host "Existing release found with this tag, skipping release creation"
    exit
} catch {
    $queryResponseStatusCode = $_.Exception.Response.StatusCode.value__
    
    # if the response is not 404 then we rethrow it
    if ($queryResponseStatusCode -ne 404) {
        throw
    }
    
    Write-Host "No existing release with this tag found"
}

$createDraftForm = @{ 
    "tag_name" = $gitTag
    "name" = "Version $($prettyVersionNumber)"
    "body" = "Version $($prettyVersionNumber)" # TODO: place git commit and date and stuff in here
    "draft" = $true
}

Write-Host "Creating github release draft"
$createDraftResponse = Invoke-RestMethod `
    -Uri "https://api.github.com/repos/Yen/PSRT.Astra/releases" `
    -Authentication OAuth `
    -Token $secureToken `
    -Method Post `
    -Body ($createDraftForm | ConvertTo-Json)

Write-Host "Uploading Astra executable asset to release $($createDraftResponse.id)"
Invoke-WebRequest `
    -Uri "https://uploads.github.com/repos/Yen/PSRT.Astra/releases/$($createDraftResponse.id)/assets?name=PSRT.Astra.exe" `
    -Authentication OAuth `
    -Token $secureToken `
    -Method Post `
    -InFile $executablePath `
    -ContentType "application/octet-stream" | Out-Null
