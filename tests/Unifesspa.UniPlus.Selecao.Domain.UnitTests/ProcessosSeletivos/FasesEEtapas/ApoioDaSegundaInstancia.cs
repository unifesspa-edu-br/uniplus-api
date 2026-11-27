namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.CenarioDeProcesso;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.DeclaracoesAdotadasDeTeste;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.RecursosParaQuemJulga;

using Veredicto = Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas.ExigenciaDasBancasDaHomologacao.Veredicto;

/// <summary>
/// O que os testes da banca de 2ª instância montam: as fases e as etapas que admitem ou não instância superior, com ou sem a
/// banca de 2ª instância, e o cenário que as reúne com o cronograma, as etapas e a configuração declarada, para conferir e
/// identificar as causas como a publicação faz.
/// </summary>
internal static class ApoioDaSegundaInstancia
{
    internal const string Fase = "AVALIACAO";

    internal const string NomeDaFase = "Fase de avaliação";

    internal const string ItemDaFalta = "cronograma_instancia_superior_com_banca_da_segunda_instancia";
    internal const string ItemDaSobra = "cronograma_banca_da_segunda_instancia_com_recurso_para_julgar";

    internal static readonly ProcedimentoDeRecursoDeclarado[] DaNota = [Procedimento(TipoMotivoDeDecisao.NotaDaEtapa)];

    internal static readonly ProcedimentoDeRecursoDeclarado[] DaHeteroidentificacao = [Procedimento(TipoMotivoDeDecisao.Heteroidentificacao)];

    /// <summary>A avaliadora de heteroidentificação cujo recurso se encerra na 1ª instância: o recurso que admite instância superior sobre o item dela é recusado.</summary>
    internal static readonly TipoDeBancaCopiado QueEncerraONaPrimeira = Avaliadora(TipoDeBanca.Heteroidentificacao, true, TipoMotivoDeDecisao.Heteroidentificacao);

    private static readonly CorretoresDasEtapas SemCorretores = new(new Dictionary<ReferenciaRegra, CorretoresDaRegra>());

    /// <summary>O cronograma, as etapas e a configuração declarada que os reúne, montados com as mesmas entidades e a mesma origem.</summary>
    internal sealed record Cenario(CronogramaDoProcesso Cronograma, EtapasDoProcesso Etapas, ConfiguracaoDeclarada Configuracao)
    {
        /// <summary>As causas da banca de 2ª instância, com os identificadores dos itens que cada uma aponta.</summary>
        internal FieldError[] Causas => [.. IdentificacaoDosItensDaCausa.Identificar(ConferenciaDaSegundaInstancia.Conferir(Cronograma, Etapas), Configuracao)];

        internal Veredicto Faltas => ConferenciaDaSegundaInstancia.Faltas(Cronograma, Etapas);

        internal Veredicto Sobras => ConferenciaDaSegundaInstancia.Sobras(Cronograma, Etapas);

        internal bool ItemEstaVerde(string codigo) => Itens().Single(item => item.Codigo == codigo).Ok;

        internal string IdentificadorDaFase(int posicao) => Configuracao.Cronograma.CronogramaFases[posicao].Codigo;

        internal string IdentificadorDaBancaDaFase(int posicaoDaFase, int posicaoDaBanca) =>
            Configuracao.Cronograma.CronogramaFases[posicaoDaFase].BancasRequeridas.ElementAt(posicaoDaBanca).Id.ToString("D");

        internal string IdentificadorDaEtapa(int posicao) => Configuracao.Avaliacao.Etapas[posicao].Id.ToString("D");

        internal string IdentificadorDaBancaDaEtapa(int posicaoDaEtapa, int posicaoDaBanca) =>
            Configuracao.Avaliacao.Etapas[posicaoDaEtapa].Bancas.ElementAt(posicaoDaBanca).Id.ToString("D");

        private IReadOnlyList<ItemConformidade> Itens() =>
            ConferenciaDasBancas.Itens(OrigemCandidatos.InscricaoPropria, Cronograma, Etapas, SemCorretores);
    }

    /// <summary>O cenário com as partes recusadas na leitura que o teste nomear (por exemplo, <c>fases[0].bancasRequeridas</c>).</summary>
    internal static Cenario Montar(ModeloDeFase[] fases, ModeloDeEtapa[] etapas, params string[] partesRecusadas)
    {
        OrigemNoDocumento origem = partesRecusadas.Length == 0 ? OrigemNoDocumento.Identidade : ComPartesRecusadas(partesRecusadas);
        FaseCronograma[] fasesAdotadas = [.. fases.Select(static fase => fase.Adotada())];
        EtapaProcesso[] etapasAdotadas = [.. etapas.Select(static etapa => etapa.Adotada())];

        return new(
            CronogramaDoProcesso.Do(fasesAdotadas, origem),
            EtapasDoProcesso.Das(etapasAdotadas, origem),
            Lida(fasesAdotadas, etapasAdotadas, origem));
    }

    internal static BancaRequerida DaSegundaInstanciaNaFase() => NaFase(TiposDeBancaCopiadosDeTeste.DeSegundaInstancia());

    internal static BancaDaEtapa DaSegundaInstanciaNaEtapa() => NaEtapa(TiposDeBancaCopiadosDeTeste.DeSegundaInstancia());

    internal static ModeloDeFase FaseSemRecurso(params BancaRequerida[] bancas) => new ModeloDeFase(Fase, 1) with { Bancas = bancas };

    internal static ModeloDeFase FaseQueAdmite(params BancaRequerida[] bancas) => FaseComRecurso(Fase, DaNota, true, bancas);

    internal static ModeloDeFase FaseQueNaoAdmite(params BancaRequerida[] bancas) => FaseComRecurso(Fase, DaNota, false, bancas);

    internal static ModeloDeEtapa EtapaSemRecurso(params BancaDaEtapa[] bancas) => EtapaNaFase(Fase) with { Bancas = bancas };

    internal static ModeloDeEtapa EtapaQueAdmite(params BancaDaEtapa[] bancas) => EtapaComRecurso("Prova", Fase, DaNota, true, bancas);

    internal static ModeloDeEtapa EtapaQueNaoAdmite(params BancaDaEtapa[] bancas) => EtapaComRecurso("Prova", Fase, DaNota, false, bancas);

    /// <summary>A etapa com dois recursos, o primeiro sem instância superior e o segundo com ela.</summary>
    internal static ModeloDeEtapa EtapaComUmRecursoSoQueAdmite(params BancaDaEtapa[] bancas)
    {
        ModeloDeEtapa semInstanciaSuperior = EtapaQueNaoAdmite();
        ModeloDeEtapa comInstanciaSuperior = EtapaQueAdmite();

        return semInstanciaSuperior with { Recursos = [.. semInstanciaSuperior.Recursos, .. comInstanciaSuperior.Recursos], Bancas = bancas };
    }

    /// <summary>A fase que admite instância superior sobre o item de heteroidentificação, que a avaliadora da etapa encerra na 1ª instância.</summary>
    internal static ModeloDeFase FaseQueAdmiteSobreItemQueSeEncerra(params BancaRequerida[] bancas) => FaseComRecurso(Fase, DaHeteroidentificacao, true, bancas);

    internal static ModeloDeEtapa EtapaComAvaliadoraQueEncerra(params BancaDaEtapa[] outrasBancas) =>
        EtapaNaFase(Fase) with { Bancas = [NaEtapa(QueEncerraONaPrimeira), .. outrasBancas] };

    internal static ModeloDeEtapa EtapaQueAdmiteSobreItemQueSeEncerra(params BancaDaEtapa[] outrasBancas) =>
        EtapaComRecurso("Prova", Fase, DaHeteroidentificacao, true, [NaEtapa(QueEncerraONaPrimeira), .. outrasBancas]);
}
