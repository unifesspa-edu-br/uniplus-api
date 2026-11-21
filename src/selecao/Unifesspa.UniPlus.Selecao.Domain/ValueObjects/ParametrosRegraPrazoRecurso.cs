namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Os parâmetros já conferidos do recurso de uma fase (<see cref="Entities.RegraRecursoFase"/>) ou de uma etapa
/// (<see cref="Entities.RecursoDaEtapa"/>): os prazos, o efeito suspensivo da 1ª instância e a guarda da vaga. Só se chega
/// a eles por <see cref="ParametrosDeclaradosDoRecurso.Conferir"/>.
/// </summary>
/// <remarks>
/// <para>
/// O recurso contra um ato fica sob a unidade responsável pela fase de origem do ato, inclusive quando chega à instância
/// superior. A 2ª instância tem prazo próprio e sempre tem efeito suspensivo; a escolha do efeito e a guarda da vaga são da
/// 1ª instância e valem pela versão vigente quando o período de recurso abriu (UNI-REQ-0093), enquanto o direito de recorrer
/// em 2ª instância e o prazo dela valem pela versão vigente quando o indeferimento é publicado ao candidato (UNI-REQ-0481).
/// </para>
/// <para>
/// Não é uma união discriminada: <see cref="Entities.RegraRecursoFase"/> só admite a regra
/// <c>RECURSO-PRAZO-ANCORADO-EM-ATO</c>, então este tipo é a única variante, sem discriminador e sem <c>switch</c>.
/// </para>
/// </remarks>
/// <param name="PrazoDeInterposicao">Prazo para apresentar o recurso na 1ª instância.</param>
/// <param name="AdmiteInstanciaSuperior">Se o recurso admite instância superior. Obrigatória e sem valor padrão: quem configura decide, para que nenhum edital seja registrado sem dizer se o recurso termina na 1ª instância.</param>
/// <param name="PrazoDaSegundaInstancia">Prazo para apresentar o recurso na 2ª instância; só existe quando o recurso admite instância superior.</param>
/// <param name="EfeitoSuspensivo">A escolha do efeito suspensivo, obrigatória, da 1ª instância.</param>
/// <param name="GuardaDaVaga">Se a vaga em disputa fica guardada; guardar até o resultado definitivo é o que vale quando o edital não declara.</param>
public sealed record ParametrosRegraPrazoRecurso(
    PrazoDoRecurso PrazoDeInterposicao,
    bool AdmiteInstanciaSuperior,
    PrazoDoRecurso? PrazoDaSegundaInstancia,
    EfeitoSuspensivo EfeitoSuspensivo,
    GuardaDaVaga GuardaDaVaga)
{
    // Para o EF: os dois prazos são entidades embutidas, e o EF não os liga a parâmetro de construtor.
    private ParametrosRegraPrazoRecurso()
        : this(null!, false, null, default, default)
    {
    }
}
