const config = require('./config');
const { initializeDatabase, closeDatabase } = require('./db');
const { createApp } = require('./app');

async function start() {
  await initializeDatabase();
  const server = createApp().listen(config.port, () => {
    console.log(`史莱姆盲盒服务器已启动：http://localhost:${config.port}`);
  });

  async function shutdown(signal) {
    console.log(`收到 ${signal}，正在关闭服务器……`);
    server.close(async () => {
      await closeDatabase();
      process.exit(0);
    });
  }

  process.on('SIGINT', () => shutdown('SIGINT'));
  process.on('SIGTERM', () => shutdown('SIGTERM'));
}

start().catch((error) => {
  console.error('服务器启动失败', error);
  process.exit(1);
});
