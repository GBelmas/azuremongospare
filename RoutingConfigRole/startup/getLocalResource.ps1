param($name)
[void]([System.Reflection.Assembly]::LoadWithPartialName("Microsoft.WindowsAzure.ServiceRuntime"))
write-host ([Microsoft.WindowsAzure.ServiceRuntime.RoleEnvironment]::GetLocalResource($name)).RootPath.TrimEnd('\\')