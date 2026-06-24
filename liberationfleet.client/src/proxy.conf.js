const { env } = require('process');

const target = env.ASPNETCORE_HTTPS_PORT ? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}` :
  env.ASPNETCORE_HTTP_PORT ? `http://localhost:${env.ASPNETCORE_HTTP_PORT}` :
  env.ASPNETCORE_URLS ? env.ASPNETCORE_URLS.split(';')[0] : 'http://localhost:5157';

const PROXY_CONFIG = [
  {
    context: [
      "/weatherforecast",
      "/api"
    ],
    target,
    secure: false,
    changeOrigin: true
  },
  {
    context: [
      "/hubs"
    ],
    target,
    secure: false,
    changeOrigin: true,
    ws: true
  }
]

module.exports = PROXY_CONFIG;
