using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;
using System.Linq;

using static UnityEngine.ParticleSystem;

struct Rule
{
  public string name;
  public float g;
  public float min;
  public float max;

  public Rule(string name, float g, float min, float max)
  {
    this.name = name;
    this.g = g;
    this.min = min;
    this.max = max;
  }

  public override readonly string ToString() => $"{name}: {g} ({min} - {max})";
}

[RequireComponent(typeof(ParticleSystem))]
public class ParticleManager : MonoBehaviour
{
  [SerializeField] Bounds bounds;
  [SerializeField] int groupSize = 100;
  [Range(0, 0.1f)][SerializeField] float forceScale = 0.05f;
  [SerializeField] List<Color> groups = new() { Color.red, Color.yellow, Color.green };
  [MinMaxSlider(0, 1)][SerializeField] Vector2 gConstraints = new(0, 1);
  [MinMaxSlider(0.25f, 3)][SerializeField] Vector2 distanceConstraints = new(0, 1);
  [SerializeField] float friction = 0.95f;

  [SerializeField] Particle[] swarm = { };

  readonly Dictionary<int, int> types = new();
  readonly Dictionary<long, Rule> rules = new();

  [ReadOnly][SerializeField] List<string> rulesList = new();

  new ParticleSystem particleSystem;

  void CreateGroups(List<Color> colors, int groupSize)
  {
    EmitParams emitParams = new()
    {
      velocity = Vector3.zero,
      startLifetime = Mathf.Infinity,
    };

    foreach (var groupColor in colors)
    {
      emitParams.startColor = groupColor;

      particleSystem.Emit(emitParams, groupSize);
    }
  }

  long Hash(int index1, int index2) => (long)index1 << 32 | (long)index2;

  Rule CreateRule(int index1, int index2, float g, float min, float max, string name)
  {
    var index = Hash(index1, index2);

    Debug.Log($"Creating rule {name} ({index:X}): {g}");

    if (rules.ContainsKey(index))
    { Debug.LogError($"Rule already exists: {index:X}: {g}"); return rules[index]; }

    var rule = new Rule(name, g, min, max);
    rules.Add(index, rule);

    return rule;
  }

  Vector3 delta;
  Vector3 direction;
  float force;
  float distance;
  Particle particle1;
  Particle particle2;
  int type1;
  int type2;
  long hash;
  Rule rule;

  float CalcForce(float distance, float g, float min, float max) =>
    Mathf.Lerp(g, 0, distance / max) - 3 * Mathf.Lerp(Mathf.Abs(g), 0, distance / min);

  void ApplyRules()
  {
    for (var i = 0; i < swarm.Length; i++)
    {
      particle1 = swarm[i];
      type1 = types[i];

      for (var j = i + 1; j < swarm.Length; j++)
      {
        particle2 = swarm[j];
        type2 = types[j];

        delta = particle2.position - particle1.position;
        distance = delta.magnitude;

        hash = Hash(type1, type2);
        rule = rules[hash];

        if (distance > rule.max) continue;

        direction = delta.normalized;

        force = CalcForce(distance, rule.g, rule.min, rule.max);
        particle1.velocity += force * forceScale * direction;
        swarm[i] = particle1;

        hash = Hash(type2, type1);
        rule = rules[hash];

        if (distance > rule.max) continue;

        force = CalcForce(distance, rule.g, rule.min, rule.max);
        particle2.velocity += force * forceScale * -direction;
        swarm[j] = particle2;
      }
    }
  }

  void MoveParticles()
  {
    for (int i = 0; i < swarm.Length; i++)
    {
      Particle particle = swarm[i];
      particle.position += particle.velocity * Time.deltaTime;

      if (!bounds.Contains(particle.position))
      {
        Vector3 delta = bounds.center - particle.position;
        particle.velocity += delta.normalized * Mathf.Lerp(0.1f, 1, delta.magnitude / bounds.extents.magnitude);
      }

      particle.velocity *= friction;

      swarm[i] = particle;
    }
  }

  [Button("Randomize Rules")]
  void CreateRules()
  {
    rules.Clear();
    rulesList.Clear();

    for (int i = 0; i < groups.Count; i++)
    {
      var c1 = groups[i];
      var index1 = groups.IndexOf(c1);

      for (int j = 0; j < groups.Count; j++)
      {
        var c2 = groups[j];
        var index2 = groups.IndexOf(c2);

        var name = $"{i}-{j}";
        var g = Random.Range(gConstraints.x, gConstraints.y) * (Mathf.Round(Random.value) * 2 - 1);
        var min = Random.Range(0.25f, distanceConstraints.x);
        var max = Random.Range(distanceConstraints.y, 3.25f);
        var newRule = CreateRule(index1, index2, g, min, max, name);

        rulesList.Add(newRule.ToString());
      }
    }
  }

  void Awake() => particleSystem = GetComponent<ParticleSystem>();

  IEnumerator Start()
  {
    CreateGroups(groups, groupSize);

    yield return new WaitForSeconds(0.1f);

    swarm = new Particle[groupSize * groups.Count];

    particleSystem.GetParticles(swarm);

    CreateRules();

    Debug.Log($"Swarm: {swarm.Length}");

    string s = "";

    for (int p = 0; p < swarm.Length; p++)
    {
      var particle = swarm[p];
      var colorIndex = groups.FindIndex(c => c == particle.startColor);
      types.Add(p, colorIndex);

      s += $"{p}: {colorIndex}\n";
    }

    Debug.Log(s);
  }

  void Update()
  {
    ApplyRules();
    MoveParticles();

    if (swarm.Length > 0)
      particleSystem.SetParticles(swarm, swarm.Length);
  }

  void OnDrawGizmosSelected()
  {
    Gizmos.color = Color.red;
    Gizmos.DrawWireCube(bounds.center, bounds.size);
  }
}