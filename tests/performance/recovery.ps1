$ErrorActionPreference = "Stop"

$Namespace = "default"
$Selector = "app.kubernetes.io/name=eurotrade"
$Deployment = "eurotrade"
$TimeoutSeconds = 120

$podsBefore = kubectl get pods `
    -n $Namespace `
    -l $Selector `
    -o jsonpath="{range .items[*]}{.metadata.name}{'\n'}{end}"

$podToDelete = ($podsBefore | Select-Object -First 1)

Write-Host "Deleting pod: $podToDelete"

$start = Get-Date
kubectl delete pod $podToDelete `
    -n $Namespace `
    --wait=false | Out-Host

$deadline = $start.AddSeconds($TimeoutSeconds)
$recovered = $false

while ((Get-Date) -lt $deadline) {

    $readyReplicas = kubectl get deployment $Deployment `
        -n $Namespace `
        -o jsonpath="{.status.readyReplicas}"

    if (-not $readyReplicas) {
        $readyReplicas = 0
    }

    $elapsed = ((Get-Date) - $start).TotalSeconds

    "{0} elapsed={1:N1}s readyReplicas={2}" -f `
        (Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"),
        $elapsed,
        $readyReplicas

    if ([int]$readyReplicas -ge 2) {
        $recovered = $true
        break
    }

    Start-Sleep -Milliseconds 500
}

$duration = ((Get-Date) - $start).TotalSeconds

if (-not $recovered) {
    Write-Host "RECOVERY_RESULT=FAIL durationSeconds=$([math]::Round($duration,2))"
    exit 1
}

Write-Host "RECOVERY_RESULT=PASS durationSeconds=$([math]::Round($duration,2))"
