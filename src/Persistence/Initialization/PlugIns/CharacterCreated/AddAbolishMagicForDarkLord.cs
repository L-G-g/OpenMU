// <copyright file="AddAbolishMagicForDarkLord.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.PlugIns.CharacterCreated;

using System.Runtime.InteropServices;
using MUnique.OpenMU.Persistence.Initialization.CharacterClasses;
using MUnique.OpenMU.Persistence.Initialization.Skills;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds the Abolish Magic skill (active in castle siege) to a created dark lord character.
/// </summary>
[Guid("7E3B0F5A-2C61-4D8E-9A47-1B5C3D9E6F20")]
[PlugIn]
[Display(Name = nameof(MUnique.OpenMU.Persistence.Initialization.Properties.PlugInResources.AddAbolishMagicForDarkLord_Name), Description = nameof(MUnique.OpenMU.Persistence.Initialization.Properties.PlugInResources.AddAbolishMagicForDarkLord_Description), ResourceType = typeof(MUnique.OpenMU.Persistence.Initialization.Properties.PlugInResources))]
public class AddAbolishMagicForDarkLord : AddInitialSkillPlugInBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddAbolishMagicForDarkLord"/> class.
    /// </summary>
    public AddAbolishMagicForDarkLord()
        : base((byte)CharacterClassNumber.DarkLord, (ushort)SkillNumber.AbolishMagic)
    {
    }
}
