import React, { useState, useEffect, useRef } from 'react';
import { 
  Send, 
  Map, 
  Shield, 
  Heart, 
  Zap, 
  Backpack, 
  History,
  Info
} from 'lucide-react';
import { motion, AnimatePresence } from 'framer-motion';
import { getNPCResponse } from '../logic/GameEngine';

function GameClient({ characters }) {
  const [messages, setMessages] = useState([
    { id: 1, sender: 'rina', text: '어서 오세요, 길드장님! 오늘은 어떤 이야기를 나눠볼까요?', type: 'npc' }
  ]);
  const [inputValue, setInputValue] = useState('');
  const [activeBuddy, setActiveBuddy] = useState('rina');
  const chatEndRef = useRef(null);

  const activeChar = characters?.find(c => c.Id === activeBuddy) || characters?.[0];

  const [isExpeditionRunning, setIsExpeditionRunning] = useState(false);

  useEffect(() => {
    chatEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  const handleSend = () => {
    if (!inputValue.trim()) return;

    const newUserMsg = { id: Date.now(), sender: 'master', text: inputValue, type: 'user' };
    setMessages(prev => [...prev, newUserMsg]);
    setInputValue('');

    // Simulate NPC delay
    setTimeout(() => {
      const npcReply = { 
        id: Date.now() + 1, 
        sender: activeBuddy, 
        text: getNPCResponse(activeBuddy, inputValue), 
        type: 'npc' 
      };
      setMessages(prev => [...prev, npcReply]);
    }, 1000);
  };

  const startExpedition = () => {
    setIsExpeditionRunning(true);
    const expeditionMsg = { id: Date.now(), sender: 'system', text: '🏹 원정대가 던전으로 출발했습니다...', type: 'system' };
    setMessages(prev => [...prev, expeditionMsg]);

    setTimeout(() => {
      const resultMsg = { 
        id: Date.now() + 1, 
        sender: 'system', 
        text: '✅ 원정 완료! [푸른 화염의 탑] 클리어. 전리품: 회복 포션 x2, 잊혀진 룬 x1.', 
        type: 'system' 
      };
      setMessages(prev => [...prev, resultMsg]);
      setIsExpeditionRunning(false);
    }, 3000);
  };

  return (
    <div className="game-layout">
      {/* Sidebar: Character Info (Inside Main Content area) */}
      <div className="game-sidebar">
        <div className="glass-card" style={{ padding: '1rem', height: '100%', display: 'flex', flexDirection: 'column' }}>
          <div style={{ textAlign: 'center', marginBottom: '1.5rem' }}>
             <div className="avatar-large" style={{ background: `linear-gradient(135deg, ${activeChar?.Id === 'kyle' ? '#ef4444' : activeChar?.Id === 'rina' ? '#22c55e' : '#f59e0b'}, #000)` }}>
               {activeChar?.Name?.[0]}
             </div>
             <h2 style={{ margin: '0.5rem 0 0' }}>{activeChar?.Name}</h2>
             <span className="tag" style={{ fontSize: '0.7rem' }}>{activeChar?.Role}</span>
          </div>

          <div className="stat-row">
            <Heart size={14} color="#ef4444" />
            <div className="stat-bar-bg"><div className="stat-bar-fill" style={{ width: `${(activeChar?.Stats?.CurrentHP / activeChar?.Stats?.MaxHP) * 100}%`, background: '#ef4444' }}></div></div>
            <span>{activeChar?.Stats?.CurrentHP}/{activeChar?.Stats?.MaxHP}</span>
          </div>
          <div className="stat-row">
            <Zap size={14} color="#3b82f6" />
            <div className="stat-bar-bg"><div className="stat-bar-fill" style={{ width: `${(activeChar?.Stats?.CurrentMP / activeChar?.Stats?.MaxMP) * 100}%`, background: '#3b82f6' }}></div></div>
            <span>{activeChar?.Stats?.CurrentMP}/{activeChar?.Stats?.MaxMP}</span>
          </div>

          <div style={{ marginTop: '1.5rem' }}>
             <h4 className="font-heading" style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem' }}>
               <Backpack size={16} /> 인벤토리
             </h4>
             <div className="inventory-grid">
                {activeChar?.Inventory?.slice(0, 8).map((item, idx) => (
                  <div key={idx} className="inventory-item" title={`${item.ItemName} (x${item.Count})`}>
                    <span style={{ fontSize: '0.7rem' }}>{item.ItemName[0]}</span>
                  </div>
                ))}
             </div>
          </div>

          <div style={{ marginTop: 'auto', paddingTop: '1rem' }}>
             <p style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', fontStyle: 'italic', lineHeight: 1.4 }}>
               "{activeChar?.Background?.slice(0, 60)}..."
             </p>
          </div>
        </div>
      </div>

      {/* Main Chat Area */}
      <div className="chat-area">
        <div className="chat-window">
          <div className="chat-messages">
            <AnimatePresence>
              {messages.map((msg) => (
                <motion.div 
                  key={msg.id}
                  initial={{ opacity: 0, y: 10, scale: 0.95 }}
                  animate={{ opacity: 1, y: 0, scale: 1 }}
                  className={`message-bubble ${msg.type}`}
                >
                  <div className="message-content">
                    {msg.type === 'npc' && <div className="message-sender">{msg.sender}</div>}
                    {msg.text}
                  </div>
                </motion.div>
              ))}
            </AnimatePresence>
            <div ref={chatEndRef} />
          </div>

          <div className="chat-input-area">
            <input 
              type="text" 
              placeholder={isExpeditionRunning ? "원정 중에는 대화할 수 없습니다..." : "메시지를 입력하세요..."} 
              disabled={isExpeditionRunning}
              value={inputValue}
              onChange={(e) => setInputValue(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && handleSend()}
            />
            <button className="send-btn" onClick={handleSend} disabled={isExpeditionRunning}>
              <Send size={20} />
            </button>
          </div>
        </div>

        <div className="game-actions">
           <button className="action-btn spotlight" onClick={startExpedition} disabled={isExpeditionRunning}>
             <Map size={18} /> {isExpeditionRunning ? '원정 중...' : '원정 보내기'}
           </button>
           <button className="action-btn">
             <Shield size={18} /> 훈련하기
           </button>
           <button className="action-btn">
             <History size={18} /> 로그 보기
           </button>
        </div>
      </div>

      {/* Right Sidebar: Party Selection */}
      <div className="party-sidebar">
        <h4 className="font-heading" style={{ marginBottom: '1rem' }}>현재 파티</h4>
        {['rina', 'kyle', 'bram'].map(id => (
          <div 
            key={id} 
            className={`party-member-card ${activeBuddy === id ? 'active' : ''}`}
            onClick={() => setActiveBuddy(id)}
          >
            <div className="avatar-small">{id[0].toUpperCase()}</div>
            <span>{id === 'rina' ? '리나' : id === 'kyle' ? '카일' : '브람'}</span>
          </div>
        ))}
        <div style={{ marginTop: 'auto', padding: '1rem', background: 'rgba(255,255,255,0.02)', borderRadius: '12px', fontSize: '0.8rem' }}>
           <Info size={14} style={{ marginBottom: '0.5rem' }} />
           <p style={{ margin: 0, color: 'var(--text-secondary)' }}>
             길드 마스터로서 동료들과 대화하고 호감도를 쌓아보세요.
           </p>
        </div>
      </div>
    </div>
  );
}

export default GameClient;
