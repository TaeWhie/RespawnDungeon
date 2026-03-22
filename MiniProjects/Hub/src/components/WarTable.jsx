import React, { useState } from 'react';
import { useGame } from '../context/GameContext';
import { motion } from 'framer-motion';
import { ArrowLeft, Sword, Shield, Map as MapIcon, Timer } from 'lucide-react';

const MISSIONS = [
  { id: 1, name: '고블린 정찰막사 습격', difficulty: 'D', reward: '100 Gold', time: '5s' },
  { id: 2, name: '지옥불 반도 수색', difficulty: 'C', reward: '250 Gold', time: '10s' },
  { id: 3, name: '심연의 동굴 탐사', difficulty: 'B', reward: '500 Gold', time: '20s' }
];

function WarTable() {
  const { setActiveScreen, setGold, setLogs } = useGame();
  const [selectedMission, setSelectedMission] = useState(null);
  const [isRunning, setIsRunning] = useState(false);

  const [missionLog, setMissionLog] = useState('');

  const startMission = () => {
    if (!selectedMission) return;
    setIsRunning(true);
    setMissionLog('백엔드 서버와 통신 중...');
    
    fetch('http://localhost:3001/api/expedition', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ missionId: selectedMission.id })
    })
      .then(res => res.json())
      .then(data => {
        if (data.status === 'success') {
          setGold(prev => prev + parseInt(selectedMission.reward));
          setLogs(prev => [`Real Mission Success: ${selectedMission.name}`, ...prev]);
          setMissionLog(data.output);
          // Auto-return after a delay or let user see output
        } else {
          setMissionLog('오류: ' + data.error);
        }
      })
      .catch(err => setMissionLog('네트워크 오류: ' + err.message))
      .finally(() => setIsRunning(false));
  };

  return (
    <div className="war-table glass-panel" style={{ width: '900px', height: '700px', padding: '3rem', position: 'relative' }}>
      <button className="hud-button" style={{ position: 'absolute', top: '1.5rem', left: '1.5rem' }} onClick={() => setActiveScreen('office')}>
        <ArrowLeft size={18} /> BACK
      </button>

      <div style={{ textAlign: 'center', marginBottom: '3rem' }}>
        <h1 className="title-text" style={{ fontSize: '2.5rem' }}>전략 작전 회의실</h1>
        <p style={{ color: 'var(--text-secondary)' }}>파티를 결성하고 새로운 임무를 수행하세요.</p>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 350px', gap: '2rem' }}>
        <div className="mission-list" style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
           {MISSIONS.map(m => (
             <div 
               key={m.id} 
               className={`mission-card glass-panel ${selectedMission?.id === m.id ? 'active' : ''}`}
               onClick={() => setSelectedMission(m)}
               style={{ 
                 padding: '1.5rem', 
                 cursor: 'pointer',
                 borderColor: selectedMission?.id === m.id ? 'var(--accent-neon)' : 'rgba(255,255,255,0.05)',
                 transition: 'all 0.2s'
               }}
             >
               <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <div>
                    <h3 style={{ marginBottom: '0.25rem' }}>{m.name}</h3>
                    <span style={{ fontSize: '0.7rem', color: 'var(--accent-neon)', fontWeight: 700 }}>DIFF: {m.difficulty}</span>
                  </div>
                  <div style={{ textAlign: 'right' }}>
                    <div style={{ fontSize: '0.9rem', color: 'var(--rpg-gold)', fontWeight: 700 }}>{m.reward}</div>
                    <div style={{ fontSize: '0.7rem' }}>{m.time}</div>
                  </div>
               </div>
             </div>
           ))}
        </div>

        <div className="mission-detail glass-panel" style={{ background: 'rgba(0,0,0,0.3)', padding: '2rem', display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
           <h4 className="title-text">DEPLOYMENT INTEL</h4>
           {selectedMission ? (
             <>
               <p style={{ fontSize: '0.85rem', lineHeight: 1.6 }}>"{selectedMission.name} 작전은 매우 위험합니다. 충분한 정비 후 출발하세요."</p>
               <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.8rem' }}>
                 <span><Timer size={14} /> Duration</span>
                 <span>{selectedMission.time}</span>
               </div>
               <button 
                className="hud-button primary" 
                style={{ marginTop: 'auto', width: '100%' }}
                onClick={startMission}
                disabled={isRunning}
               >
                 {isRunning ? 'MISSION IN PROGRESS...' : 'START MISSION'}
               </button>
             </>
           ) : (
             <p style={{ color: 'var(--text-secondary)', fontSize: '0.8rem' }}>임무를 선택해 주세요.</p>
           )}

           {missionLog && (
             <div className="glass-panel" style={{ height: '200px', overflowY: 'auto', padding: '1rem', fontSize: '0.7rem', background: 'rgba(0,0,0,0.5)', marginTop: '1rem', whiteSpace: 'pre-wrap', fontFamily: 'monospace' }}>
               <h5 style={{ color: 'var(--accent-neon)', marginBottom: '0.5rem' }}>BACKEND CONSOLE</h5>
               {missionLog}
             </div>
           )}
        </div>
      </div>
    </div>
  );
}

export default WarTable;
