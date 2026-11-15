// <copyright file="SpectreDialogues.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Spectres;

/// <summary>
/// What the rivals of the spectre hub say when the player talks to them ("Los Héroes Olvidados"): they were legends of the
/// server, but time made them forget their memories, their power and their names. Each duel wakes up a part of who they were.
/// </summary>
/// <remarks>
/// Each kind has ten texts: the texts 1 to 9 come before the nine duels (the unlock and the eight evolutions), the text 10
/// is the end, where the rival remembers its name (<c>{0}</c>, see <see cref="SpectreNames"/>).
/// </remarks>
public static class SpectreDialogues
{
    /// <summary>
    /// The number of texts of each kind.
    /// </summary>
    public const int TextCount = SpectreProgress.MaximumVictories + 1;

    private static readonly string[][] Texts =
    [

        // Guerrero — El héroe de todos
        [
            "Otra vez alguien viene a luchar... Supongo que no tengo nada mejor que hacer. Hace mucho que nadie me busca.",
            "Tu forma de pelear me resulta familiar. Antes había muchos guerreros como tú por aquí... aunque ya casi no recuerdo sus rostros.",
            "Qué extraño... Por un instante creí escuchar a alguien gritando mi nombre. Había mucha gente a mi alrededor, y todos parecían felices.",
            "¡Ja! Hacía años que no disfrutaba tanto una pelea. Empiezo a recordar aquellos días en que nunca combatía solo.",
            "¿Sabes? Nunca fui el más fuerte ni el mejor equipado. Pero siempre había alguien esperándome cuando entraba al servidor.",
            "¡Ahora lo recuerdo! Dondequiera que fuera, todos me saludaban. Incluso mis enemigos terminaban convirtiéndose en amigos.",
            "¡Qué tiempos aquellos! Organizábamos aventuras, ayudábamos a los nuevos y celebrábamos cada victoria como si fuera la primera.",
            "Gracias por seguir viniendo, amigo. Pensé que nadie volvería a recordarme, pero tú me hiciste sentir parte de este mundo otra vez.",
            "¡Una última batalla, compañero! Quiero que conozcas al guerrero que alguna vez hizo sonreír a todo un servidor.",
            "¡Ya lo recuerdo todo! No fui el más poderoso, pero fui el guerrero más querido de este servidor. ¡Mi nombre es {0}, y gracias a ti mi historia no será olvidada!",
        ],

        // Mago — El poder perdido
        [
            "¿Has venido a desafiarme? Qué pérdida de tiempo... Apenas puedo recordar cómo lanzar un hechizo.",
            "Esa energía... Algo se ha despertado dentro de mí. Hace mucho tiempo, la magia respondía a cada uno de mis pensamientos.",
            "Recuerdo una torre, cientos de hechizos y jugadores observándome en silencio. ¿Por qué me miraban con tanto asombro?",
            "¡Interesante! Mis hechizos comienzan a recuperar su fuerza. Tal vez todavía quede algo del mago que fui.",
            "Hubo una época en la que ningún enemigo podía resistir mi magia. Mi poder era tan grande que algunos pensaban que era imposible derrotarme.",
            "¡Ahora entiendo aquellas miradas! Los jugadores me desafiaban desde todos los rincones del servidor, buscando ser los primeros en vencerme.",
            "¡Magnífico! Gracias a nuestras batallas, siento cómo regresa mi verdadero poder. Hacía siglos que nadie me obligaba a esforzarme tanto.",
            "Debo reconocértelo, joven. Tienes algo que muchos de mis antiguos rivales nunca tuvieron: el valor de volver a intentarlo.",
            "¡Prepárate! Por última vez sentirás la magia que hizo temblar a este servidor. ¡No pienso contenerme, amigo mío!",
            "¡Mi memoria ha regresado! Fui el mago más poderoso que jamás pisó este servidor, y mi nombre era temido por todos. ¡Soy {0}, y tú has despertado mi leyenda!",
        ],

        // Elfa — La guardiana olvidada
        [
            "¿Necesitas ayuda? Lo siento... Ya no sé cómo ayudar a nadie. Hasta mi magia parece haberme abandonado.",
            "Verte luchar ha despertado algo en mí. Recuerdo que antes siempre había alguien a quien proteger.",
            "Escucho voces lejanas pidiendo curación y bendiciones. Es extraño... Esos recuerdos me hacen sentir menos sola.",
            "¡Ah, ahora lo recuerdo! Mis flechas no eran mi mayor fortaleza. Había algo mucho más importante que podía ofrecer a mis compañeros.",
            "Siempre me buscaban antes de una gran batalla. Una bendición mía podía cambiar el destino de un combate entero.",
            "¡Qué hermoso era verlos regresar victoriosos! Nunca me importó ser quien derrotara al enemigo, sino asegurarme de que todos volvieran con vida.",
            "¡Ya puedo sentir mi magia nuevamente! Durante años fui la compañera que todos querían tener a su lado. Y ahora comprendo por qué.",
            "Has luchado tanto para ayudarme a recordar... Ojalá pudiera darte todas las bendiciones que alguna vez compartí con este mundo.",
            "¡Ven, valiente amigo! Luchemos una vez más, no como enemigos, sino como dos compañeros que se ayudan a ser mejores.",
            "¡He recuperado mi luz! Fui la elfa que protegió a incontables guerreros, el mejor soporte de todo el servidor. ¡Soy {0}, y siempre tendrás mi bendición!",
        ],

        // Gladiador — El duelista legendario
        [
            "¿Otro retador? Haz lo que quieras... Ya ni recuerdo cuándo fue la última vez que gané un duelo.",
            "Tienes buenos movimientos. Antes solía enfrentarme a guerreros como tú, aunque ninguno de sus nombres permanece en mi memoria.",
            "Al cruzar nuestras armas, he recordado una arena llena de espectadores. Todos esperaban que comenzara una batalla... ¿Sería la mía?",
            "¡Ja, ja! ¡Eso estuvo bien! Mis reflejos empiezan a regresar. Hacía mucho que no sentía esta emoción.",
            "Recuerdo haber pasado incontables horas perfeccionando cada movimiento. Para mí, un duelo no era solo una pelea, era un arte.",
            "¡Ahora lo veo con claridad! Los mejores combatientes del servidor hacían fila para desafiarme. Y uno tras otro caían derrotados.",
            "¡Por los dioses, qué buenos tiempos! Había ganado tantos duelos que algunos pensaban que jamás conocería la derrota. ¡Pero tú estás logrando hacerme sudar!",
            "Eres un digno rival, amigo. No importa cuántas veces crucemos nuestras armas, cada combate contigo me hace sentir más vivo.",
            "¡Desenvaina y prepárate! Esta será nuestra batalla más grande. ¡Quiero que recuerdes por qué alguna vez fui el rey de los duelos!",
            "¡Mi espíritu de combate ha despertado! Fui el mejor duelista del servidor, el gladiador al que todos soñaban derrotar. ¡Mi nombre es {0}, y ha sido un honor luchar contigo!",
        ],

        // Lord — El líder sin ejército
        [
            "Mi castillo está vacío y mis estandartes han caído. Si buscas una batalla, adelante... Ya no tengo nada que defender.",
            "El sonido de nuestras armas me resulta familiar. Antes, cuando luchaba, siempre había otros guerreros a mi alrededor.",
            "Recuerdo un gran salón lleno de voces y risas. Había decenas de personas esperando mis órdenes... ¿Quiénes eran?",
            "¡Por mi estandarte! Algo despierta dentro de mí. Recuerdo haber dirigido ejércitos hacia grandes batallas.",
            "Mi clan... ¡Tenía un clan! Éramos una familia de guerreros, magos y elfas que luchábamos bajo una misma bandera.",
            "¡Ahora recuerdo nuestras conquistas! Cada día llegaban nuevos miembros y nuestros estandartes aparecían en todos los rincones del servidor.",
            "¡Ja, ja! ¡Qué gloriosos días! Llegamos a ser el clan más grande que este mundo había conocido, y yo tenía el honor de liderarlo.",
            "Un verdadero líder nunca olvida a quienes lucharon a su lado. Tú me has recordado esa lección, y por eso te considero uno de los míos.",
            "¡Levanta tu arma, camarada! Aunque mis ejércitos ya no estén aquí, mi espíritu de líder sigue intacto. ¡Luchemos por el honor de los que ya no están!",
            "¡Mis recuerdos han vuelto! Fui el Lord que lideró el clan más numeroso del servidor, un ejército unido por la lealtad. ¡Soy {0}, y desde hoy siempre tendrás un lugar entre mis filas!",
        ],

        // Invocadora — La belleza olvidada
        [
            "¿Qué deseas de mí? Mis invocaciones ya no responden y mi reflejo se ha desvanecido... No queda nada digno de admirar.",
            "Qué curioso... Al verte luchar, he recordado una época en la que nunca estaba sola. Siempre había alguien acercándose para hablar conmigo.",
            "Recuerdo luces, vestidos elegantes y personas reuniéndose a mi alrededor. Todos parecían conocerme, aunque yo ya no recuerdo a ninguno.",
            "¡Oh! Mis invocaciones comienzan a escucharme otra vez. Tal vez aún conserve un poco de aquella magia que tanto fascinaba a los demás.",
            "Ahora recuerdo los paseos por las ciudades del servidor. Los jugadores se detenían a mirarme, y algunos me seguían solo para poder saludarme.",
            "¡Qué recuerdos tan dulces! Decían que ninguna joya, armadura o hechizo podía igualar mi belleza. Aunque debo admitir que disfrutaba bastante de tanta atención.",
            "¡Ja, ja! Creo que estoy recuperando mi antiguo encanto. Pero tú eres diferente... Has vuelto una y otra vez sin pedirme nada a cambio.",
            "Antes todos admiraban mi apariencia, pero pocos se detenían a conocerme. Tú me has dado algo mucho más valioso: una razón para sonreír nuevamente.",
            "¡Mi querido amigo, bailemos una última vez en el campo de batalla! Quiero mostrarte toda la magia y la belleza que este mundo había olvidado.",
            "¡Por fin recuerdo quién soy! Fui la invocadora más bella de todo el servidor, admirada en cada ciudad y recordada por mi encanto. ¡Mi nombre es {0}, y jamás olvidaré a quien me devolvió la sonrisa!",
        ],

        // Peleador — El maestro de los novatos
        [
            "¿Quieres pelear conmigo? No sé si valga la pena... Hace tanto tiempo que nadie necesita mi ayuda.",
            "Tu manera de combatir me recuerda a alguien. Antes pasaba mucho tiempo enseñando a otros a dar sus primeros golpes.",
            "Recuerdo las afueras de una ciudad, llenas de aventureros principiantes. Siempre había alguien pidiéndome consejos.",
            "¡Bien hecho! Ese último movimiento fue excelente. ¡Ja! Parece que todavía conservo la costumbre de enseñar mientras peleo.",
            "Ahora recuerdo que muchos jugadores me buscaban cuando recién comenzaban. Les enseñaba a luchar, los acompañaba y a veces les regalaba mi propio equipo.",
            "¡Qué alegría recordar sus primeras victorias! Algunos de aquellos novatos terminaron siendo grandes guerreros. Y nunca dejaron de saludarme cuando nos cruzábamos.",
            "¡Ya lo entiendo! No era famoso por ganar torneos ni por tener el mejor equipo. Todos me conocían porque jamás dejaba a un principiante sin ayuda.",
            "Estoy orgulloso de ti, muchacho. Has mejorado en cada encuentro, y acompañarte me ha devuelto la felicidad que sentía al enseñar.",
            "¡Vamos, campeón! Quiero ver todo lo que has aprendido. ¡Demuestra que estás listo para convertirte en el héroe que algún día ayudará a otros!",
            "¡Ahora recuerdo mi propósito! Fui el peleador que acompañaba a los novatos y los ayudaba a convertirse en grandes aventureros. ¡Soy {0}, y verte crecer ha sido mi mayor victoria!",
        ],
    ];

    /// <summary>
    /// Gets what the rival of a kind says to a player which beat it a number of times.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="victories">The victories of the player against the rival: 0 to 8 for the texts before the duels, 9 for the end.</param>
    /// <param name="name">The name of the spectre, which the rival remembers at the end.</param>
    /// <returns>The text.</returns>
    public static string GetText(SpectreKind kind, int victories, string name)
    {
        var texts = Texts[(int)kind];
        var index = Math.Clamp(victories, 0, texts.Length - 1);
        return index == texts.Length - 1 ? string.Format(System.Globalization.CultureInfo.InvariantCulture, texts[index], name) : texts[index];
    }
}
