using System.Runtime.Loader;
using Anduril.Core.Skills;
using Microsoft.Extensions.Logging;

namespace Anduril.Skills;

/// <summary>
/// Skill runner for compiled C# skills that implement <see cref="ISkill"/>.
/// Loads skill assemblies from the plugins directory using <see cref="AssemblyLoadContext"/>
/// for isolation.
/// </summary>
public class CompiledSkillRunner(ILogger<CompiledSkillRunner> logger, string pluginsDirectory = "plugins")
    : ISkillRunner
{
    private readonly Dictionary<string, ISkill> _skills = new(StringComparer.OrdinalIgnoreCase);

    public string SkillType => "compiled";

    public Task<IReadOnlyList<SkillInfo>> GetSkillsAsync(CancellationToken cancellationToken = default)
    {
        var infos = _skills.Values.Select(s => new SkillInfo
        {
            Name = s.Name,
            Description = s.Description,
            SkillType = SkillType,
            Triggers = s.Triggers
        }).ToList();

        return Task.FromResult<IReadOnlyList<SkillInfo>>(infos);
    }

    public bool CanHandle(SkillInfo skill) => skill.SkillType == SkillType;

    public async Task<SkillResult> ExecuteAsync(
        string skillName, SkillContext context, CancellationToken cancellationToken = default)
    {
        if (!_skills.TryGetValue(skillName, out var skill))
        {
            return SkillResult.Fail($"Compiled skill '{skillName}' not found.");
        }

        try
        {
            var result = await skill.ExecuteAsync(context, cancellationToken);
            logger.LogDebug("Compiled skill '{Skill}' completed", skillName);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Compiled skill '{Skill}' failed", skillName);
            return SkillResult.Fail($"Skill '{skillName}' failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Registers a pre-instantiated compiled skill (e.g., from DI).
    /// </summary>
    public void Register(ISkill skill)
    {
        _skills[skill.Name] = skill;
        logger.LogInformation("Registered compiled skill '{Name}'", skill.Name);
    }

    /// <summary>
    /// Loads all ISkill implementations from assemblies in the plugins directory.
    /// Each assembly is loaded into its own <see cref="AssemblyLoadContext"/> for isolation.
    /// Contexts are not collectible since plugins are loaded once at startup and not hot-reloaded.
    /// </summary>
    public void LoadFromDirectory()
    {
        if (!Directory.Exists(pluginsDirectory))
        {
            logger.LogDebug("Plugins directory not found: {Path}", pluginsDirectory);
            return;
        }

        string[] dlls = Directory.GetFiles(pluginsDirectory, "*.dll", SearchOption.TopDirectoryOnly);

        foreach (string dll in dlls)
        {
            try
            {
                // isCollectible: false because plugins are loaded once at startup and never unloaded
                var context = new AssemblyLoadContext(Path.GetFileNameWithoutExtension(dll), isCollectible: false);
                var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(dll));

                var skillTypes = assembly.GetExportedTypes()
                    .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ISkill).IsAssignableFrom(t));

                foreach (var type in skillTypes)
                {
                    if (Activator.CreateInstance(type) is ISkill skill)
                    {
                        Register(skill);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load plugin from {Path}", dll);
            }
        }
    }
}

