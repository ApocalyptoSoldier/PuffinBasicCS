$sourceFolder = "$($env:REPOS)\PuffinBASIC\src\main\java\org\puffinbasic"
$targetFolder = "$($env:REPOS)\PuffinBASICCS\"

pushd $sourceFolder
$sourceFiles = gci -af -recurse *.java | % {
	[PSCustomObject]@{
		BaseName = $_.BaseName
		RelativeParent = [System.IO.Path]::GetRelativePath($pwd.Path, $_.Fullname) | Split-Path -Parent
	}
}
popd

pushd $targetFolder
$targetFiles = gci -af *.cs

$folderMappings = $sourceFiles | Group-Object 'BaseName' -AsHashTable #-NoElement

$targetFiles | % {
	$targetDirectory = $folderMappings[$_.BaseName].RelativeParent
	
	if ($targetDirectory)
	{
		if (-not (Test-Path $targetDirectory))
		{
			md $targetDirectory
		}
		
		mv $_.Name "$targetDirectory\$($_.Name)"
		if (Test-Path "$($_.BaseName).warning")
		{
			mv "$($_.BaseName).warning" "$targetDirectory\$($_.BaseName).warning"
		}
	}
}

popd
