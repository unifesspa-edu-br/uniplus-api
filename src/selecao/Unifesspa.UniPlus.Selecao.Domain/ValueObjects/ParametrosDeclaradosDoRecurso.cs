namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Os parâmetros do recurso como o edital os declara, de fase ou de etapa, com cada valor ausente ou não e cada código como
/// foi recebido. É daqui que o domínio confere uma só vez o que é declaração própria do recurso, venha o dado do pedido, do
/// documento da configuração ou do registro da versão publicada, e recusa todas as causas juntas.
/// </summary>
/// <param name="PrazoDeInterposicao">Prazo para apresentar o recurso na 1ª instância.</param>
/// <param name="AdmiteInstanciaSuperior">Se o recurso admite instância superior. Obrigatória e sem valor padrão.</param>
/// <param name="PrazoDaSegundaInstancia">Prazo para apresentar o recurso na 2ª instância: obrigatório com instância superior, recusado sem ela.</param>
/// <param name="EfeitoSuspensivo">Código do efeito suspensivo da 1ª instância; obrigatório.</param>
/// <param name="GuardaDaVaga">Código da guarda da vaga; ausente vale guardar até o resultado definitivo.</param>
public sealed record ParametrosDeclaradosDoRecurso(
    PrazoDeclarado PrazoDeInterposicao,
    bool AdmiteInstanciaSuperior,
    PrazoDeclarado PrazoDaSegundaInstancia,
    string? EfeitoSuspensivo,
    string? GuardaDaVaga)
{
    private const string PrazoValor = "prazoValor";
    private const string PrazoUnidade = "prazoUnidade";
    private const string SegundaInstanciaValor = "prazoSegundaInstanciaValor";
    private const string SegundaInstanciaUnidade = "prazoSegundaInstanciaUnidade";

    /// <summary>
    /// As recusas, acumuladas na ordem: prazo de interposição, efeito suspensivo, guarda não declarável, prazo da 2ª instância
    /// e "não guarda" com instância superior. Sem recusa, os parâmetros conferidos; com recusa, nenhum.
    /// </summary>
    public (ParametrosRegraPrazoRecurso? Conferidos, IReadOnlyList<RecusaDosParametrosDoRecurso> Recusas) Conferir()
    {
        RecusaDosParametrosDoRecurso[] recusas = [.. TodasAsRecusas().OfType<RecusaDosParametrosDoRecurso>()];

        return recusas.Length > 0 ? (null, recusas) : (Conferidos(), []);
    }

    private IEnumerable<RecusaDosParametrosDoRecurso?> TodasAsRecusas()
    {
        yield return PrazoDeInterposicao.Recusa(PrazoValor, PrazoUnidade, "prazo de interposição");
        yield return RecusaDoEfeito();
        yield return RecusaDaGuardaNaoDeclaravel();
        yield return RecusaDoPrazoDaSegundaInstancia();
        yield return RecusaDeNaoGuardarComInstanciaSuperior();
    }

    private RecusaDosParametrosDoRecurso? RecusaDoEfeito() =>
        CatalogoDe<EfeitoSuspensivo>.TentarObter(EfeitoSuspensivo, out _)
            ? null
            : new(MotivoDaRecusaDosParametros.EfeitoSuspensivoNaoDeclarado, "efeitoSuspensivo", EfeitoSuspensivo is null
                ? "Declare o efeito suspensivo do recurso na 1ª instância. Opção aceita: sem efeito suspensivo."
                : "O efeito suspensivo declarado não é uma das opções aceitas. Opção aceita: sem efeito suspensivo.");

    private RecusaDosParametrosDoRecurso? RecusaDaGuardaNaoDeclaravel() =>
        GuardaConferida() is null
            ? new(MotivoDaRecusaDosParametros.GuardaDaVagaNaoDeclaravel, "guardaDaVaga",
                "A guarda da vaga não é uma das opções aceitas: guardar a vaga até o resultado definitivo ou não guardar a vaga. "
                + "Sem a guarda informada, vale guardar até o resultado definitivo.")
            : null;

    private RecusaDosParametrosDoRecurso? RecusaDoPrazoDaSegundaInstancia() =>
        AdmiteInstanciaSuperior ? RecusaDoPrazoComInstanciaSuperior() : RecusaDoPrazoSemInstanciaSuperior();

    private RecusaDosParametrosDoRecurso? RecusaDoPrazoComInstanciaSuperior() =>
        PrazoDaSegundaInstancia.Valor is null
            ? new(MotivoDaRecusaDosParametros.PrazoSegundaInstanciaNaoDeclarado, SegundaInstanciaValor,
                "O recurso admite instância superior: informe o prazo para recorrer em 2ª instância, com valor e unidade (dias úteis ou horas).")
            : PrazoDaSegundaInstancia.Recusa(SegundaInstanciaValor, SegundaInstanciaUnidade, "prazo da 2ª instância");

    private RecusaDosParametrosDoRecurso? RecusaDoPrazoSemInstanciaSuperior() => PrazoDaSegundaInstancia switch
    {
        { Valor: not null } => PrazoSemInstanciaSuperiorEm(SegundaInstanciaValor),
        { Unidade: not null } => PrazoSemInstanciaSuperiorEm(SegundaInstanciaUnidade),
        _ => null,
    };

    private static RecusaDosParametrosDoRecurso PrazoSemInstanciaSuperiorEm(string campo) => new(
        MotivoDaRecusaDosParametros.PrazoSegundaInstanciaSemInstanciaSuperior,
        campo,
        "O recurso não admite instância superior: retire o prazo da 2ª instância ou marque que o recurso admite instância superior.");

    // A guarda recusada não é julgada contra a instância superior: a recusa dela depende de um dado que já foi recusado.
    private RecusaDosParametrosDoRecurso? RecusaDeNaoGuardarComInstanciaSuperior() =>
        AdmiteInstanciaSuperior && GuardaConferida() is Enums.GuardaDaVaga.NaoGuarda
            ? new(MotivoDaRecusaDosParametros.NaoGuardaComInstanciaSuperior, "guardaDaVaga",
                "O recurso admite instância superior, e a 2ª instância sempre tem efeito suspensivo: "
                + "escolha guardar a vaga até o resultado definitivo ou retire a instância superior.")
            : null;

    private GuardaDaVaga? GuardaConferida() =>
        GuardaDaVaga is null
            ? Enums.GuardaDaVaga.AteOResultadoDefinitivo
            : CatalogoDe<GuardaDaVaga>.TentarObter(GuardaDaVaga, out GuardaDaVaga guarda) ? guarda : null;

    private ParametrosRegraPrazoRecurso? Conferidos() =>
        PrazoDeInterposicao.ComoPrazo() is { } interposicao
            && CatalogoDe<EfeitoSuspensivo>.TentarObter(EfeitoSuspensivo, out EfeitoSuspensivo efeito)
            && GuardaConferida() is { } guarda
            ? new ParametrosRegraPrazoRecurso(
                interposicao, AdmiteInstanciaSuperior, AdmiteInstanciaSuperior ? PrazoDaSegundaInstancia.ComoPrazo() : null, efeito, guarda)
            : null;
}
