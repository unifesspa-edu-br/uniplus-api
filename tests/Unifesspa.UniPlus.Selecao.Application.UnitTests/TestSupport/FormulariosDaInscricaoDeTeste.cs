namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Um formulário que o teste declara na fase que coleta inscrição, com os itens e os termos dele: a versão publicada
/// guarda cada item e cada termo ligados ao formulário que os coleta.
/// </summary>
/// <param name="Codigo">O código do formulário.</param>
/// <param name="Obrigatorio">Se o formulário é obrigatório; o facultativo não é da inscrição.</param>
/// <param name="Itens">Os códigos dos fatos que o formulário coleta, na ordem.</param>
/// <param name="Termos">Os códigos dos termos que o formulário exige.</param>
internal sealed record FormularioDaInscricaoDeTeste(string Codigo, bool Obrigatorio, IReadOnlyList<string> Itens, IReadOnlyList<string> Termos)
{
    /// <summary>A versão publicada de teste com os formulários dados, todos ligados à fase que coleta inscrição.</summary>
    public static ConfiguracaoPublicada ConfiguracaoCom(params FormularioDaInscricaoDeTeste[] formularios)
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS 2026 — Formulários da inscrição", out Guid faseDaInscricao);

        return new ConfiguracaoPublicada(
            new CronogramaDeclarado(ConfiguracaoPublicadaDeTeste.Localidade, null, [.. processo.CronogramaFases]),
            new AvaliacaoDeclarada([.. processo.Etapas], [.. processo.CriteriosDesempate], processo.Classificacao!),
            new VagasDeclaradas([.. processo.DistribuicaoVagas], 1, processo.Fada is not null,
                processo.Fada, null, processo.AtendimentoEspecializado!),
            new ExigenciasDeclaradas([.. processo.DocumentosExigidos], []),
            ColetaDosFormularios(formularios, faseDaInscricao, CronogramaDoProcesso.Do(processo.CronogramaFases)),
            new DivulgacaoETaxaDeclaradas(null, null), null);
    }

    /// <summary>O envelope reidratado com a versão publicada de <see cref="ConfiguracaoCom"/>.</summary>
    public static EnvelopeReidratado EnvelopeCom(params FormularioDaInscricaoDeTeste[] formularios) => new(
        ConfiguracaoCom(formularios),
        DadosEdital.Criar(
            "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), Guid.CreateVersion7()).Value!,
        new string('a', 64), "America/Belem", retificacao: null, conformidade: null, new DadosDeCatalogoDaConfiguracaoPublicada(FatosDerivadosCongelados: new Dictionary<string, FatoDerivadoCongelado>()));

    private static ColetaDeclarada ColetaDosFormularios(FormularioDaInscricaoDeTeste[] formularios, Guid faseDaInscricao, CronogramaDoProcesso cronograma)
    {
        FormularioProcesso[] declarados = [.. formularios.Select(formulario => formulario.Declarar(faseDaInscricao, cronograma))];

        return new ColetaDeclarada(
            [.. formularios.Zip(declarados, static (formulario, declarado) => formulario.ItensNo(declarado)).SelectMany(static itens => itens)],
            [], declarados,
            [.. formularios.Zip(declarados, static (formulario, declarado) => formulario.TermosNo(declarado)).SelectMany(static termos => termos)],
            [], [], null);
    }

    private IEnumerable<FatoColetado> ItensNo(FormularioProcesso declarado) => Itens.Select((fato, ordem) => Item(fato, ordem).NoFormulario(declarado));

    private IEnumerable<TermoExigidoFormulario> TermosNo(FormularioProcesso declarado) => Termos.Select(termo => Termo(termo, Codigo).NoFormulario(declarado));

    private FormularioProcesso Declarar(Guid faseDaInscricao, CronogramaDoProcesso cronograma) => FormularioProcesso.Criar(
        new FormularioDeclarado(CodigoDoFormulario.Criar(Codigo).Value!, faseDaInscricao, Obrigatorio), cronograma, null,
        [
            PaginaDoFormulario.Criar(new PaginaEstrutura(FormularioDeTeste.Secao, 0, TipoPaginaFormulario.Secao, null), $"Dados do formulário {Codigo}", null, null).Value!,
            PaginaDoFormulario.Criar(new PaginaEstrutura("REVISAO", 1, TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite), "Revisão e aceite", null, null).Value!,
        ]).Value!;

    private static FatoColetado Item(string fato, int ordem) => FatoColetadoDeTeste.Coletando(fato, ordem)
        .DeTexto("LIVRE")
        .ComObrigatoriedade(Obrigatoriedade.Sempre)
        .NaSecao(FormularioDeTeste.Secao)
        .Construir();

    private static TermoExigidoFormulario Termo(string codigo, string formulario) => TermoExigidoFormulario.Criar(
        codigo, 0,
        new VersaoTermoEscolhida(Guid.CreateVersion7(), Guid.CreateVersion7(), $"TERMO_{codigo}", 1, $"Termo {codigo} do formulário {formulario}", "Declaro que é verdade.", "Lei 9.784/1999", FormaAceite.RegistroSemEnderecoIp, new string('a', 64)),
        null, Obrigatoriedade.Sempre).Value!;
}
