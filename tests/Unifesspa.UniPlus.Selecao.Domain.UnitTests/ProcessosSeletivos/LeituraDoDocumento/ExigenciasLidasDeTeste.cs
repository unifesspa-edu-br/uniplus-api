namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.DocumentoLidoDeTeste;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.EtapasLidasDeTeste;

/// <summary>
/// As exigências, as regras de comprovação e os cadastros que os testes da leitura das exigências montam: o que não declaram tem o
/// valor de uma exigência completa e conforme, geral, obrigatória e sem restrição de formato.
/// </summary>
internal static class ExigenciasLidasDeTeste
{
    internal const string Identidade = "IDENTIDADE";
    internal const string Residencia = "COMPROVANTE_RESIDENCIA";
    internal const string FaseDeInscricao = "inscricao";
    internal const string FaseDeResultado = "resultado";
    internal const string Formulario = "INSCRICAO";

    internal static CadastrosCitadosNoDocumento CadastrosDasExigencias() => new(LidoEm)
    {
        VagasEExigencias = new()
        {
            TiposDeDocumento = new Dictionary<string, CopiaDoTipoDeDocumento>
            {
                [Identidade] = Tipo(Identidade, "Documento de identidade"),
                [Residencia] = Tipo(Residencia, "Comprovante de residência"),
            },
        },
    };

    internal static FaseNoDocumento[] Fases() => [Fase(0, FaseDeInscricao, "INSCRICAO"), Fase(1, FaseDeResultado, "RESULTADO")];

    internal static ExigenciasLidas Ler(ExigenciasNoDocumento? bloco, FaseNoDocumento[] fases, EtapaNoDocumento[] etapas)
    {
        FasesLidas fasesLidas = LeituraDasFases.Ler(fases, Cadastros());
        EtapasLidas etapasLidas = LeituraDasEtapas.Ler(etapas, fasesLidas, CadastrosDasEtapas());

        return ExigenciasLidas.Ler(new(Identificacao(), fases) { Etapas = etapas, Exigencias = bloco }, CadastrosDasExigencias(), fasesLidas, etapasLidas);
    }

    internal static ExigenciasLidas Ler(ExigenciasNoDocumento? bloco) => Ler(bloco, Fases(), [Etapa(0, "prova", fase: FaseDeInscricao)]);

    internal static ExigenciasLidas Ler(params ExigenciaNoDocumento[] exigencias) => Ler(new ExigenciasNoDocumento(exigencias, []));

    internal static ExigenciasLidas Ler(ExigenciaNoDocumento[] exigencias, params RegraDeComprovacaoNoDocumento[] regras) =>
        Ler(new ExigenciasNoDocumento(exigencias, regras));

    internal static ExigenciaNoDocumento Exigencia(int posicao, string chave, string? tipo = Identidade, string? fase = FaseDeInscricao, string? etapa = null) =>
        new(posicao, chave, tipo, fase, etapa, Formulario)
        {
            Aplicabilidade = Aplicabilidade.Geral,
            Obrigatorio = true,
            FormatosPermitidos = new(true, []),
        };

    internal static ExigenciaNoDocumento Condicional(this ExigenciaNoDocumento exigencia, params CondicaoSobreDadoNoDocumento[][] alternativas) =>
        exigencia with { Aplicabilidade = Aplicabilidade.Condicional, Condicoes = alternativas };

    internal static ExigenciaNoDocumento ComBases(this ExigenciaNoDocumento exigencia, params BaseLegalNoDocumento[] bases) => exigencia with { BasesLegais = bases };

    internal static ExigenciaNoDocumento ComFormatos(this ExigenciaNoDocumento exigencia, FormatosPermitidosNoDocumento? formatos) => exigencia with { FormatosPermitidos = formatos };

    internal static CondicaoSobreDadoNoDocumento Condicao(string? dado, string? operador, JsonElement? valor) => new(dado, operador, valor);

    internal static JsonElement Texto(string valor) => JsonSerializer.SerializeToElement(valor);

    internal static BaseLegalNoDocumento Base(int posicao, string? referencia = "Norma de exemplo, art. 1º") =>
        new(posicao, referencia, TipoAbrangencia.Federal, StatusBaseLegal.Resolvido, null);

    internal static RegraDeComprovacaoNoDocumento Folha(int posicao, string? exigenciaChave) => new(posicao, FormaDeComprovacao.Documento, exigenciaChave);

    internal static RegraDeComprovacaoNoDocumento Grupo(int posicao, FormaDeComprovacao? forma, params RegraDeComprovacaoNoDocumento[] itens) =>
        new(posicao, forma, null) { Itens = itens };

    internal static RegraDeComprovacaoNoDocumento ComQuantidade(this RegraDeComprovacaoNoDocumento regra, int? quantidade) => regra with { QuantidadeMinima = quantidade };

    internal static (string? Ponteiro, string Codigo)[] Causas(ExigenciasLidas lidas) =>
        [.. lidas.Origem.PartesRecusadas.Select(static parte => (parte.Causa.Field, parte.Causa.Error.Code))];

    internal static string[] Mensagens(ExigenciasLidas lidas) => [.. lidas.Origem.PartesRecusadas.Select(static parte => parte.Causa.Error.Message)];

    private static CopiaDoTipoDeDocumento Tipo(string codigo, string nome) =>
        new(Guid.CreateVersion7(), codigo, nome, "PESSOAL", ProtecaoDoDocumentoDeTeste.De(ClassificacaoDeProtecao.Pessoal));
}
