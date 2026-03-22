import React, { createContext, useContext, useState, useCallback, useEffect } from 'react';
import { apiGet } from '../api';

const GameContext = createContext();

export const GameProvider = ({ children }) => {
  const [state, setState] = useState({
    configDirectory: '',
    characters: [],
    parties: [],
    jobs: [],
    logs: [],
  });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const refreshState = useCallback(async () => {
    setError(null);
    try {
      const data = await apiGet('/api/state');
      setState({
        configDirectory: data.configDirectory ?? '',
        characters: data.characters ?? [],
        parties: data.parties ?? [],
        jobs: data.jobs ?? [],
        logs: data.logs ?? [],
      });
    } catch (e) {
      setError(e.message || String(e));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    refreshState();
  }, [refreshState]);

  const value = {
    ...state,
    loading,
    error,
    refreshState,
  };

  return <GameContext.Provider value={value}>{children}</GameContext.Provider>;
};

export const useGame = () => useContext(GameContext);
