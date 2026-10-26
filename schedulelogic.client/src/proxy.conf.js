const PROXY_CONFIG = [
  {
    context: ["/api", "/admin"],
    target: "https://localhost:7098",
    secure: false
  }
];
module.exports = PROXY_CONFIG;