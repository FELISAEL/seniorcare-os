# Despliegue en Kubernetes

Los manifiestos usan imágenes locales con prefijo `seniorcare/`.

## Construir imágenes

```powershell
.\deploy\k8s\build-images.ps1
```

Para Minikube:

```powershell
minikube image load seniorcare/identity-api:1.0.0
minikube image load seniorcare/care-api:1.0.0
minikube image load seniorcare/emergency-api:1.0.0
minikube image load seniorcare/communication-api:1.0.0
minikube image load seniorcare/analytics-api:1.0.0
minikube image load seniorcare/etl-worker:1.0.0
minikube image load seniorcare/portal-web:1.0.0
```

## Aplicar

```powershell
kubectl apply -f .\deploy\k8s
kubectl get pods -n seniorcare
```

## Acceso local

```powershell
kubectl port-forward -n seniorcare service/portal-web 8088:80
```

Abrir `http://localhost:8088`. El mismo portal sirve login y paneles por rol.
Antes de producción se deben reemplazar secretos, configurar HTTPS, persistencia
gestionada, dominio y políticas de red.
