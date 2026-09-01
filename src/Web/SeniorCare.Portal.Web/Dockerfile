FROM nginx:alpine

# views/ contiene únicamente HTML.
COPY src/Web/SeniorCare.Portal.Web/views/ /usr/share/nginx/html/

# public/ contiene recursos estáticos (CSS, JS y PWA).
COPY src/Web/SeniorCare.Portal.Web/public/ /usr/share/nginx/html/

# El service worker se sirve bajo el alcance del panel, aunque su fuente vive en public/.
COPY src/Web/SeniorCare.Portal.Web/public/pwa/adulto-mayor/service-worker.js /usr/share/nginx/html/panel/adulto-mayor/service-worker.js

COPY infra/nginx/default.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
