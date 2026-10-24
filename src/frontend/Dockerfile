# ============ BUILD ============
FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json* ./
RUN npm ci

COPY . .
RUN npm run build

# ============ RUNTIME (nginx) ============
FROM nginx:1.27-alpine AS final
# nginx imaji /etc/nginx/templates/*.template dosyalarini acilista envsubst ile isler (SECURITY_API_KEY).
COPY nginx.conf /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
    CMD wget -qO- http://127.0.0.1/ >/dev/null || exit 1
