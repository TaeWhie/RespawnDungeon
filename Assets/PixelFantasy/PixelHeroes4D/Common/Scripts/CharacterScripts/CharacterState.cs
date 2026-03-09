namespace Assets.PixelFantasy.PixelHeroes4D.Common.Scripts.CharacterScripts
{
    /// <summary>
    /// Animation state. The same parameter controls animation transitions.
    /// </summary>
    public enum CharacterState
    {
        Idle = 0,
        Ready = 1,
        Walk = 2,
        Run = 3,
        Jump = 4,
        Fall = 5,
        Land = 6,
        Block = 7,
        Climb = 8,
        Die = 9,
        Wink = 10
    }
}