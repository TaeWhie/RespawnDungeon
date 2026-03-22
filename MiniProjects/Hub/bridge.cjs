/**
 * 레거시: JSON 파일 직접 읽기 + dotnet 원정만 실행.
 * 전체 콘솔 기능(대화 1·2, 캐릭터 생성 등)은 GuildDialogue `dotnet run -- --hub-api` + Vite 프록시를 사용하세요.
 */
const express = require('express');
const cors = require('cors');
const bodyParser = require('body-parser');
const fs = require('fs');
const path = require('path');
const { exec } = require('child_process');

const app = express();
const port = 3001;

app.use(cors());
app.use(bodyParser.json());

const PROJECT_DIR = 'e:\\My project (1)\\MiniProjects\\GuildDialogue';
const CHARS_FILE = path.join(PROJECT_DIR, 'Config', 'CharactersDatabase.json');
const LOGS_FILE = path.join(PROJECT_DIR, 'Config', 'ActionLog.json');

// GET /api/state
app.get('/api/state', (req, res) => {
  try {
    const chars = JSON.parse(fs.readFileSync(CHARS_FILE, 'utf8'));
    let logs = [];
    if (fs.existsSync(LOGS_FILE)) {
      const logsData = JSON.parse(fs.readFileSync(LOGS_FILE, 'utf8'));
      logs = logsData.ActionLog || [];
    }
    res.json({ characters: chars, status: 'ok', logs: logs.slice(-20) });
  } catch (err) {
    console.error(err);
    res.status(500).json({ error: err.message });
  }
});

// POST /api/expedition
app.post('/api/expedition', (req, res) => {
  console.log('Running REAL dotnet expedition simulation...');
  // dotnet run -- --gen-actionlog --replace-actionlog
  exec('dotnet run -- --gen-actionlog --replace-actionlog', { cwd: PROJECT_DIR }, (error, stdout, stderr) => {
    if (error) {
       console.error(`exec error: ${error}`);
       return res.status(500).json({ error: stderr || error.message });
    }
    console.log(`stdout: ${stdout}`);
    
    try {
      const chars = JSON.parse(fs.readFileSync(CHARS_FILE, 'utf8'));
      const logsData = JSON.parse(fs.readFileSync(LOGS_FILE, 'utf8'));
      res.json({ 
        status: 'success', 
        output: stdout, 
        characters: chars,
        newLogs: (logsData.ActionLog || []).slice(-10) 
      });
    } catch (readErr) {
      res.status(500).json({ error: 'Failed to read updated logs: ' + readErr.message });
    }
  });
});

// POST /api/recruit
app.post('/api/recruit', (req, res) => {
   const newChar = req.body;
   try {
     const chars = JSON.parse(fs.readFileSync(CHARS_FILE, 'utf8'));
     chars.push(newChar);
     fs.writeFileSync(CHARS_FILE, JSON.stringify(chars, null, 2));
     res.json({ status: 'success', characters: chars });
   } catch (err) {
     res.status(500).json({ error: err.message });
   }
});

app.listen(port, () => {
  console.log(`RPG Bridge server listening at http://localhost:${port}`);
});
