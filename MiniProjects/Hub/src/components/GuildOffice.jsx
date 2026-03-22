import React from 'react';
import { useGame } from '../context/GameContext';
import { 
  MessageSquare, 
  UserPlus, 
  Map as MapIcon, 
  Settings as SettingsIcon,
  Shield,
  Coins
} from 'lucide-react';
import { motion } from 'framer-motion';

function GuildOffice() {
  const { currentBuddy, setActiveScreen, gold, characters, setBuddyId, buddyId } = useGame();

  return (
    <div className="office-screen">
      {/* Top HUD */}
      <div className="game-hud">
        <div className="glass-panel" style={{ padding: '0.5rem 1.5rem', display: 'flex', gap: '2rem', alignItems: 'center' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <Coins size={18} color="var(--rpg-gold)" />
            <span style={{ fontWeight: 700, color: 'var(--rpg-gold)' }}>{gold} GOLD</span>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <Shield size={18} color="var(--rpg-blue)" />
            <span style={{ fontWeight: 700 }}>LVL 5 GUILD</span>
          </div>
        </div>
        
        <h1 className="title-text" style={{ fontSize: '1.5rem' }}>GUILD MASTER COMMAND</h1>

        <button className="hud-button" onClick={() => alert('Settings coming soon!')}>
          <SettingsIcon size={18} />
        </button>
      </div>

      {/* Center: Buddy Portrait */}
      <div className="buddy-view">
        <motion.div 
          className="buddy-portrait"
          key={currentBuddy?.Id}
          initial={{ y: 20, opacity: 0 }}
          animate={{ y: 0, opacity: 1 }}
          transition={{ type: 'spring', stiffness: 100 }}
          style={{ 
            borderColor: currentBuddy?.Id === 'kyle' ? '#ef4444' : currentBuddy?.Id === 'rina' ? '#22c55e' : '#f59e0b',
            boxShadow: `0 20px 60px -10px ${currentBuddy?.Id === 'kyle' ? 'rgba(239, 68, 68, 0.3)' : currentBuddy?.Id === 'rina' ? 'rgba(34, 197, 94, 0.3)' : 'rgba(245, 158, 11, 0.3)'}`
          }}
        >
          {currentBuddy?.Name?.[0] || '?'}
          <div className="portrait-glow"></div>
        </motion.div>
        
        <motion.div 
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          style={{ textAlign: 'center', cursor: 'pointer' }}
          onClick={() => {
            const idx = characters.findIndex(c => c.Id === buddyId);
            const nextIdx = (idx + 1) % characters.length;
            setBuddyId(characters[nextIdx].Id);
          }}
          title="Click to switch hero"
        >
          <div style={{ fontSize: '0.7rem', color: 'var(--text-secondary)', marginBottom: '0.25rem' }}>CLICK TO SWITCH HERO</div>
          <h2 style={{ fontSize: '2.5rem', marginBottom: '0.25rem' }} className="title-text">
            {currentBuddy?.Name || 'No Buddy Selected'}
          </h2>
          <p style={{ color: 'var(--accent-neon)', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.1em' }}>
            {currentBuddy?.Role || 'Recruit new members'}
          </p>
        </motion.div>
      </div>

      {/* Bottom Actions */}
      <div className="bottom-actions">
        <button className="hud-button primary" onClick={() => setActiveScreen('chat')}>
          <MessageSquare size={20} /> 대화하기
        </button>
        <button className="hud-button" onClick={() => setActiveScreen('recruit')}>
          <UserPlus size={20} /> 신규 영입
        </button>
        <button className="hud-button" onClick={() => setActiveScreen('expedition')}>
          <MapIcon size={20} /> 원정 계획
        </button>
      </div>
    </div>
  );
}

export default GuildOffice;
