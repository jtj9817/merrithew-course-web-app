# syntax=docker/dockerfile:1
#
# Multi-stage build for the Course Inquiry Dashboard: a single ASP.NET Core
# process that serves the Web API, the Razor shell, and the compiled React
# island over a local SQLite file (see README.md / docs/architecture).
#
#   1. frontend  - build the React island with Vite -> backend/wwwroot/app
#   2. build     - restore + publish the ASP.NET Core app (island included)
#   3. runtime   - slim aspnet image, non-root, SQLite on a mounted volume

# ---- Stage 1: build the React island -------------------------------------
# Vite 8 needs a current Node LTS; the repo ships a pnpm-lock.yaml (v9).
FROM node:22-bookworm-slim AS frontend
WORKDIR /src
RUN npm install -g pnpm@9

# Install deps first so this layer caches unless the lockfile changes.
COPY frontend/package.json frontend/pnpm-lock.yaml ./frontend/
RUN cd frontend && pnpm install --frozen-lockfile

# `pnpm build` runs `tsc -b && vite build`; vite.config.ts emits the hashed
# assets and a manifest to ../backend/wwwroot/app (i.e. /src/backend/wwwroot/app).
COPY frontend ./frontend
RUN cd frontend && pnpm build

# ---- Stage 2: restore + publish the ASP.NET Core app ----------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# global.json pins the SDK feature band; copy it so the image matches local builds.
COPY global.json ./
COPY backend/CourseInquiryDashboard.csproj backend/
RUN dotnet restore backend/CourseInquiryDashboard.csproj

COPY backend/ backend/
# Bring in the compiled island so `publish` captures wwwroot/app.
COPY --from=frontend /src/backend/wwwroot/app backend/wwwroot/app
RUN dotnet publish backend/CourseInquiryDashboard.csproj -c Release -o /app/publish --no-restore

# The static-web-assets pipeline drops dot-prefixed folders, so .vite/manifest.json
# is not published. The Razor shell reads that manifest to resolve the island's
# hashed entry/CSS (backend/Hosting/ViteManifest.cs); restore it, or /dashboard
# silently degrades to the "JavaScript required" shell.
RUN cp -r backend/wwwroot/app/.vite /app/publish/wwwroot/app/

# ---- Stage 3: runtime -----------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Kestrel listens on 8080 inside the container (the aspnet image's default port).
ENV ASPNETCORE_URLS=http://+:8080
# Keep the SQLite file out of the ephemeral container layer: /data is a volume,
# so inquiries survive `docker compose up`/redeploys. Migrations run at startup.
ENV ConnectionStrings__DefaultConnection="Data Source=/data/inquiries.db"

# Hand the data dir to the image's built-in non-root user (UID 1654).
RUN mkdir -p /data && chown $APP_UID:$APP_UID /data
VOLUME ["/data"]

COPY --from=build /app/publish .

EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "CourseInquiryDashboard.dll"]
