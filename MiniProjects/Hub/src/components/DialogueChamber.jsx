import React, { useState, useRef, useEffect } from 'react';
import { useGame } from '../context/GameContext';
import { motion, AnimatePresence } from 'framer-motion';
import { ArrowLeft, Send } from 'lucide-react';

function DialogueChamber() {
  const { setActiveScreen, currentBuddy, logs } = useGame();
  
  const formatLog = (log) => {
    switch(log.EventType) {
      case 'partyForm': return `Party formed: ${log.PartyMembers?.join(', ')}`;
      case 'questAccept': return `Accepted mission at ${log.Location}`;
      case 'dungeonEnter': return `Entered ${log.DungeonName}`;
      case 'combat': return `${log.ActorId || 'Party'} engaged in combat at ${log.Location}`;
      case 'loot': return `Acquired: ${log.LootItems?.map(i => i.ItemName).join(', ')}`;
      case 'outcome': return `Mission ${log.Outcome === 'clear' ? 'SUCCESS' : 'FAILURE'}`;
      default: return log.Dialogue || `${log.EventType} event at ${log.Location}`;
    }
  };

  const [messages, setMessages] = useState([
    { id: 'welcome', type: 'npc', text: `길드장님, ${currentBuddy?.Name} 입니다. 무엇을 도와드릴까요?` },
    ...logs.slice(-5).map((l, i) => ({
      id: `log-${i}`, type: 'npc', text: `[과거 기록] ${formatLog(l)}`
    }))
  ]);
  const [input, setInput] = useState('');
  const scrollRef = useRef();

  useEffect(() => {
    scrollRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  const handleSend = () => {
    if (!input.trim()) return;
    setMessages(prev => [...prev, { id: Date.now(), type: 'user', text: input }]);
    setInput('');
    
    // Simple mock response
    setTimeout(() => {
      setMessages(prev => [...prev, { id: Date.now() + 1, type: 'npc', text: '흐음... 흥미로운 제안이군요. 하지만 조금 더 생각해 봐야 할 것 같습니다.' }]);
    }, 1000);
  };

  return (
    <div className="dialogue-screen glass-panel" style={{ width: '1000px', height: '80vh', display: 'flex' }}>
      {/* Left: Character Portrait Mini */}
      <div style={{ width: '300px', borderRight: '1px solid var(--rpg-border)', padding: '2rem', textAlign: 'center' }}>
         <div style={{ width: '150px', height: '150px', borderRadius: '75px', background: 'rgba(255,255,255,0.1)', margin: '0 auto 1rem', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: '3rem', fontWeight: 900 }}>
           {currentBuddy?.Name?.[0]}
         </div>
         <h2 className="title-text">{currentBuddy?.Name}</h2>
         <p style={{ fontSize: '0.8rem', color: 'var(--accent-neon)' }}>{currentBuddy?.Role}</p>
         <hr style={{ margin: '1.5rem 0', borderColor: 'var(--rpg-border)' }}/>
         <div style={{ textAlign: 'left', fontSize: '0.8rem', color: 'var(--text-secondary)', lineHeight: 1.6 }}>
           {currentBuddy?.Background}
         </div>
         <button className="hud-button" style={{ marginTop: '2rem', width: '100%' }} onClick={() => setActiveScreen('office')}>
           <ArrowLeft size={16} /> EXIT TALK
         </button>
      </div>

      {/* Right: Chat Window */}
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
        <div style={{ flex: 1, padding: '2rem', overflowY: 'auto' }}>
           <AnimatePresence>
             {messages.map(m => (
               <motion.div 
                 key={m.id}
                 initial={{ opacity: 0, x: m.type === 'npc' ? -10 : 10 }}
                 animate={{ opacity: 1, x: 0 }}
                 style={{ 
                   display: 'flex', 
                   justifyContent: m.type === 'npc' ? 'flex-start' : 'flex-end',
                   marginBottom: '1rem' 
                 }}
               >
                 <div style={{ 
                   maxWidth: '70%', 
                   padding: '1rem', 
                   background: m.type === 'npc' ? 'rgba(255,255,255,0.05)' : 'var(--rpg-blue)',
                   borderRadius: '15px',
                   border: m.type === 'npc' ? '1px solid var(--rpg-border)' : 'none',
                   fontSize: '0.95rem'
                 }}>
                   {m.text}
                 </div>
               </motion.div>
             ))}
           </AnimatePresence>
           <div ref={scrollRef} />
        </div>

        <div style={{ padding: '1.5rem', background: 'rgba(0,0,0,0.2)', display: 'flex', gap: '1rem' }}>
           <input 
             type="text" 
             className="rpg-input" 
             placeholder="Discuss with the buddy..."
             value={input}
             onChange={(e) => setInput(e.target.value)}
             onKeyDown={(e) => e.key === 'Enter' && handleSend()}
           />
           <button className="send-btn" onClick={handleSend} style={{ width: '60px', borderRadius: '12px', background: 'var(--rpg-blue)', border: 'none', color: 'white', cursor: 'pointer' }}>
             <Send size={20} />
           </button>
        </div>
      </div>
    </div>
  );
}

export default DialogueChamber;
