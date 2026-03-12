using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TriInspector;

[CreateAssetMenu(fileName ="SimpleRandomWalkParameters_",menuName = "PCG/SimpleRAndomWalkData")]
public class SimpleRandomWalkSO : ScriptableObject
{
    [Title("Simple Random Walk 파라미터")]
    [Slider(1, 100)]
    public int iterations = 10;
    [Slider(1, 50)]
    public int walkLength = 10;
    public bool startRandomlyEachIteration = true;
}
