import React, { useState } from 'react';
import { useGame } from '../context/GameContext';
import { motion } from 'framer-motion';
import { ArrowLeft, UserPlus, Shield, Zap, Heart, Sparkles } from 'lucide-react';

function RecruitmentCenter() {
  const { setActiveScreen, addCharacter, setGold, gold } = useGame();
  const [name, setName] = useState('');
  const [role, setRole] = useState('Vanguard');
  const [isGenerating, setIsGenerating] = useState(false);

  const handleRecruit = () => {
    if (!name || gold < 200) return;

    setIsGenerating(true);
    setTimeout(() => {
      const newChar = {
        Id: name.toLowerCase().replace(/\s/g, '-'),
        Name: name,
        Role: role,
        Background: `${name}은(는) 머나먼 동방에서 온 실력 있는 ${role}입니다. 길드 마스터의 부름을 받고 이곳에 도달했습니다.`,
        Stats: {
          MaxHP: 100,
          CurrentHP: 100,
          MaxMP: 50,
          CurrentMP: 50,
          Attack: 15,
          Defense: 10
        },
        Inventory: []
      };
      
      // Real API Call to bridge
      fetch('http://localhost:3001/api/recruit', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(newChar)
      })
        .then(res => res.json())
        .then(data => {
          if (data.status === 'success') {
            addCharacter(newChar);
            setGold(prev => prev - 200);
            setActiveScreen('office');
          } else {
            alert('Error recruiting: ' + data.error);
          }
        })
        .catch(err => alert('Network error: ' + err.message))
        .finally(() => setIsGenerating(false));
    }, 1500);
  };

  return (
    <div className="recruit-screen glass-panel" style={{ width: '800px', height: '600px', padding: '3rem', position: 'relative' }}>
      <button className="hud-button" style={{ position: 'absolute', top: '1.5rem', left: '1.5rem' }} onClick={() => setActiveScreen('office')}>
        <ArrowLeft size={18} /> BACK
      </button>

      <div style={{ textAlign: 'center', marginBottom: '3rem' }}>
        <h1 className="title-text" style={{ fontSize: '2.5rem' }}>영입 사무소</h1>
        <p style={{ color: 'var(--text-secondary)' }}>새로운 영웅을 길드에 초대하세요 (비용: 200 GOLD)</p>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '3rem' }}>
        <div className="recruit-form" style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
          <div>
            <label style={{ display: 'block', marginBottom: '0.5rem', fontSize: '0.8rem', fontWeight: 700, color: 'var(--accent-neon)' }}>HERO NAME</label>
            <input 
              type="text" 
              className="rpg-input" 
              placeholder="Enter name..." 
              value={name}
              onChange={(e) => setName(e.target.value)}
            />
          </div>

          <div>
            <label style={{ display: 'block', marginBottom: '0.5rem', fontSize: '0.8rem', fontWeight: 700, color: 'var(--accent-neon)' }}>SELECT CLASS</label>
            <div className="class-selector">
              {['Vanguard', 'Support', 'Scavenger', 'Mage'].map(r => (
                <div 
                  key={r} 
                  className={`class-card ${role === r ? 'active' : ''}`}
                  onClick={() => setRole(r)}
                >
                  {r}
                </div>
              ))}
            </div>
          </div>
        </div>

        <div className="recruit-preview glass-panel" style={{ background: 'rgba(0,0,0,0.3)', padding: '1.5rem' }}>
          <h4 style={{ marginBottom: '1rem', borderBottom: '1px solid var(--rpg-border)', paddingBottom: '0.5rem' }}>CAPABILITY ESTIMATE</h4>
          <div className="stat-preview">
            <div className="stat-line"><Heart size={14} color="var(--rpg-red)"/> HP: 100 <div className="bar"><div className="fill red"></div></div></div>
            <div className="stat-line"><Zap size={14} color="var(--rpg-blue)"/> MP: 50 <div className="bar"><div className="fill blue"></div></div></div>
            <div className="stat-line"><Shield size={14} color="var(--rpg-gold)"/> DEF: 10 <div className="bar"><div className="fill gold"></div></div></div>
          </div>
          
          <div style={{ marginTop: '2rem', padding: '1rem', background: 'rgba(255,255,255,0.02)', borderRadius: '10px', fontSize: '0.8rem', color: 'var(--text-secondary)' }}>
             <Sparkles size={14} style={{ marginBottom: '0.5rem' }} />
             <p>AI가 캐릭터의 서사와 스킬을 생성할 준비가 되었습니다.</p>
          </div>
        </div>
      </div>

      <div style={{ marginTop: 'auto', display: 'flex', justifyContent: 'center', paddingTop: '2rem' }}>
        <button 
          className="hud-button primary" 
          style={{ padding: '1rem 3rem', fontSize: '1.1rem' }}
          onClick={handleRecruit}
          disabled={!name || gold < 200 || isGenerating}
        >
          {isGenerating ? 'GENERATING...' : <><UserPlus size={20} /> HIRE HERO</>}
        </button>
      </div>
    </div>
  );
}

export default RecruitmentCenter;
