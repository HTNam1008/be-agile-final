@echo off
cd /d "C:\Users\LENOVO\Downloads\moe-backend-template-with-auth-plan\moe-backend-template-auth-plan"
"C:\Program Files\dotnet\dotnet.exe" run --project src\Hosts\Moe.StudentFinance.Api\Moe.StudentFinance.Api.csproj --launch-profile Moe.StudentFinance.Api > "C:\Users\LENOVO\Downloads\moe-backend-template-with-auth-plan\moe-backend-template-auth-plan\.manual-test-logs\api.log" 2>&1
