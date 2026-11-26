namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

/// <summary>
/// As três opções de efeito suspensivo no documento congelado: cada uma é gravada com o código e o rótulo, é lida de volta
/// como o edital a declarou, e a escolha faz parte do resumo de integridade, que muda de uma opção para outra.
/// </summary>
public sealed class EfeitoSuspensivoNoEnvelopeTests
{
    public static TheoryData<EfeitoSuspensivo> Opcoes()
    {
        TheoryData<EfeitoSuspensivo> opcoes = [];
        foreach (EfeitoSuspensivo opcao in Enum.GetValues<EfeitoSuspensivo>())
        {
            opcoes.Add(opcao);
        }

        return opcoes;
    }

    [Theory(DisplayName = "O envelope grava o código da opção no parâmetro e o rótulo no contexto")]
    [MemberData(nameof(Opcoes))]
    public void Serializar_GravaCodigoERotulo(EfeitoSuspensivo efeito)
    {
        JsonObject parametros = SerializacaoDoCronograma.SerializarParametrosRegraPrazoRecurso(ParametrosCom(efeito));

        parametros["efeitoSuspensivo"]!.GetValue<string>().Should().Be(efeito.Codigo());
        parametros["contexto"]!["efeitoSuspensivoRotulo"]!.GetValue<string>().Should().Be(efeito.Rotulo());
    }

    [Theory(DisplayName = "A leitura do envelope devolve a opção que foi gravada")]
    [MemberData(nameof(Opcoes))]
    public void Ler_DevolveAOpcaoGravada(EfeitoSuspensivo efeito)
    {
        JsonObject parametros = ContextoDosRotulos.SemContexto(
            SerializacaoDoCronograma.SerializarParametrosRegraPrazoRecurso(ParametrosCom(efeito))).AsObject();
        LeitorEnvelope leitor = new();

        ParametrosDeclaradosDoRecurso? lidos = LeituraDosPrazosDeRecurso.LerParametrosDePrazoDeRecurso(leitor, parametros, "parametros");

        leitor.Falhou.Should().BeFalse();
        lidos!.Conferir().Conferidos!.EfeitoSuspensivo.Should().Be(efeito);
    }

    [Fact(DisplayName = "O resumo de integridade muda de uma opção para outra e se repete para a mesma opção")]
    public void Resumo_MudaEntreAsOpcoes()
    {
        string[] resumos = [.. Enum.GetValues<EfeitoSuspensivo>().Select(static efeito => ResumoCom(efeito))];

        resumos.Should().OnlyHaveUniqueItems();
        ResumoCom(EfeitoSuspensivo.ParaTodosOsRecursos).Should().Be(resumos[0]);
    }

    private static string ResumoCom(EfeitoSuspensivo efeito) => HashCanonicalComputer.ComputeSha256Hex(
        PerfilCanonicoV1.Instancia.Serializar(SerializacaoDoCronograma.SerializarParametrosRegraPrazoRecurso(ParametrosCom(efeito))));

    private static ParametrosRegraPrazoRecurso ParametrosCom(EfeitoSuspensivo efeito) => new(
        new PrazoDoRecurso(48m, UnidadePrazo.Horas), AdmiteInstanciaSuperior: false, PrazoDaSegundaInstancia: null, efeito, GuardaDaVaga.AteOResultadoDefinitivo);
}
