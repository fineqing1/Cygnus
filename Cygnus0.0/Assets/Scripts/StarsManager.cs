using System.Collections.Generic;
using UnityEngine;

public class StarsManager : MonoBehaviour
{
    // 非单例，随场景卸载而销毁，不跨场景保存

    [SerializeField] List<Star> stars = new List<Star>(); // 可在 Inspector 中拖拽赋值，序列化保存
}
