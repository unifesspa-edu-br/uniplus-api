namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura do bloco de identidade do certame: dados do edital, tipo de processo, identificador legível, identidade da unidade e apresentação dos pressupostos.
/// </summary>
internal static class LeituraDoEdital
{
    /// <summary>
    /// Lê e fecha a forma do tipo autocontido. O agregado vivo já carrega a
    /// mesma cópia por valor e a recanonicalização posterior prova a paridade;
    /// o decoder não consulta Configuração para validar este dado histórico.
    /// </summary>
    internal static void LerTipoProcesso(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject tipo = leitor.Objeto(payload, "tipoProcesso", "$");
        if (leitor.Falhou)
        {
            return;
        }

        leitor.ExigirChaves(tipo, "tipoProcesso", "origemId", "codigo", "nome");
        leitor.Identificador(tipo, "origemId", "tipoProcesso");
        leitor.TextoNaoVazio(tipo, "codigo", "tipoProcesso");
        leitor.TextoNaoVazio(tipo, "nome", "tipoProcesso");
    }

    /// <summary>
    /// Lê o identificador legível congelado com a versão. Nulo é forma aceita — a versão congelada
    /// sem ele —, mas um texto presente passa pela mesma regra do cadastro: um valor que o
    /// value object recusaria não sai do encoder, então achá-lo é sinal de bytes que não vieram
    /// desse caminho.
    /// </summary>
    internal static IdentificadorLegivel? LerIdentificadorLegivel(LeitorEnvelope leitor, JsonObject payload)
    {
        string? valor = leitor.TextoOpcional(payload, "identificadorLegivel", "$", FormatoKebab.ComprimentoMaximo);
        if (leitor.Falhou || valor is null)
        {
            return null;
        }

        Result<IdentificadorLegivel> identificador = IdentificadorLegivel.Criar(valor);
        return identificador.IsFailure
            ? leitor.Propagar<IdentificadorLegivel?>(identificador.Error!)
            : identificador.Value;
    }

    internal static DadosEdital? LerDadosEdital(LeitorEnvelope leitor, JsonObject payload, out string hashDocumento)
    {
        hashDocumento = string.Empty;

        JsonObject periodo = leitor.Objeto(payload, "periodo", "$");
        leitor.ExigirChaves(periodo, "periodo", "numero", "inicio", "fim");
        string? numero = leitor.TextoOpcional(periodo, "numero", "periodo", LimitesDoEnvelope.NumeroDoAto);
        DateTimeOffset inicio = leitor.Instante(periodo, "inicio", "periodo");
        DateTimeOffset fim = leitor.Instante(periodo, "fim", "periodo");

        JsonObject hashes = leitor.Objeto(payload, "hashesEdital", "$");
        leitor.ExigirChaves(hashes, "hashesEdital", "documentoEditalId", "hashSha256", "anexos");
        Guid documentoId = leitor.Identificador(hashes, "documentoEditalId", "hashesEdital");
        string hash = leitor.Texto(hashes, "hashSha256", "hashesEdital");
        IReadOnlyList<AnexoDoEdital> anexos = LerAnexosDoEdital(leitor, hashes);

        if (leitor.Falhou)
        {
            return null;
        }

        hashDocumento = hash;

        Result<DadosEdital> dados = DadosEdital.Criar(numero, inicio, fim, documentoId, anexos);
        return dados.IsFailure ? leitor.Propagar<DadosEdital>(dados.Error!) : dados.Value;
    }

    /// <summary>
    /// Os anexos do edital, na ordem em que o envelope os traz. O tipo de mídia é um dos aceitos para
    /// o documento a publicar e o hash é SHA-256 em hexadecimal minúsculo: o que o encoder emite a
    /// partir de um documento confirmado nunca sai disso.
    /// </summary>
    internal static List<AnexoDoEdital> LerAnexosDoEdital(LeitorEnvelope leitor, JsonObject hashes)
    {
        List<AnexoDoEdital> anexos = [];
        JsonArray array = leitor.Array(hashes, "anexos", "hashesEdital");
        if (leitor.Falhou)
        {
            return anexos;
        }

        for (int i = 0; i < array.Count; i++)
        {
            string path = $"hashesEdital.anexos[{i}]";
            JsonObject item = leitor.ItemObjeto(array, i, path);
            AnexoDoEdital? anexo = leitor.Falhou ? null : LerAnexo(leitor, item, path);
            if (anexo is null)
            {
                return anexos;
            }

            anexos.Add(anexo);
        }

        return anexos;
    }

    private static AnexoDoEdital? LerAnexo(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, "anexoId", "documentoId", "nomeArquivo", "tipoDeMidia", "tamanhoBytes", "hashSha256");
        Guid anexoId = leitor.Identificador(item, "anexoId", path);
        Guid documentoId = leitor.Identificador(item, "documentoId", path);
        string nome = leitor.TextoNaoVazio(item, "nomeArquivo", path, AnexoDoEdital.NomeArquivoMaxLength);
        string tipoDeMidia = leitor.Texto(item, "tipoDeMidia", path);
        int tamanho = leitor.Inteiro(item, "tamanhoBytes", path);
        string hash = leitor.Texto(item, "hashSha256", path);
        if (leitor.Falhou)
        {
            return null;
        }

        FormatoDeDocumento? formato = FormatosDeDocumento.DoTipoDeMidia(tipoDeMidia);
        return formato is { } formatoValido && LeituraDasExigencias.EhSha256Hexadecimal(hash) && tamanho > 0
            ? new AnexoDoEdital(anexoId, documentoId, nome, formatoValido, tamanho, hash)
            : leitor.Recusar<AnexoDoEdital?>(
                $"'{path}' com tipo de mídia fora dos aceitos, tamanho que não é positivo ou hash que não é SHA-256 em hexadecimal minúsculo.");
    }

    /// <summary>
    /// A identidade da Unidade administradora: lida e
    /// validada para fechar a gramática, mas <b>não é propagada</b> — mesmo raciocínio de
    /// <c>origemCandidatos</c> em <see cref="LeituraDoCronograma.LerCronogramaFases"/>. É atributo de raiz do
    /// agregado (<c>ProcessoSeletivo.UnidadeAdministradoraOrigemId</c>/<c>UnidadeAdministradora</c>),
    /// imutável após a criação (nenhum <c>Definir*</c> a altera, não há operação de re-bind) — a
    /// prova de fidelidade do round-trip vem de <c>ProcessoSeletivo.SombraParaVerificacao()</c>,
    /// que já copia os dois campos direto da raiz viva, nunca do envelope decodificado.
    /// </summary>
    /// <remarks>
    /// A cidade (ADR-0090) é opcional all-or-nothing: nula para processos
    /// congelados sem cidade, trio completo e coerente para os demais. O decoder
    /// reconfirma a bicondicional e o formato via <see cref="ReferenciaCidadeGeo.Validar"/> —
    /// mesmo raciocínio de <see cref="LeituraDaDivulgacaoETaxa.LerDivulgacao"/>/<see cref="LeituraDaDivulgacaoETaxa.LerTaxaInscricao"/>:
    /// um trio parcial nunca sai do encoder (<c>UnidadeAdministradoraSnapshot</c> só existe com
    /// cidade completa ou nenhuma), então achar um é sinal de bytes que não vieram desse caminho.
    /// </remarks>
    internal static void LerIdentidadesUnidade(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "identidadesUnidade", "$");
        if (leitor.Falhou)
        {
            return;
        }

        leitor.ExigirChaves(bloco, "identidadesUnidade", "administradora");
        JsonObject administradora = leitor.Objeto(bloco, "administradora", "identidadesUnidade");
        if (leitor.Falhou)
        {
            return;
        }

        const string path = "identidadesUnidade.administradora";
        leitor.ExigirChaves(administradora, path, "origemId", "sigla", "slug", "nome", "tipo", "cidadeCodigoIbge", "cidadeNome", "cidadeUf");

        leitor.Identificador(administradora, "origemId", path);
        leitor.TextoNaoVazio(administradora, "sigla", path, LimitesDoEnvelope.UnidadeAdministradoraSigla);
        leitor.TextoNaoVazio(administradora, "slug", path, LimitesDoEnvelope.UnidadeAdministradoraSlug);
        leitor.TextoNaoVazio(administradora, "nome", path, LimitesDoEnvelope.UnidadeAdministradoraNome);
        leitor.TextoNaoVazio(administradora, "tipo", path, LimitesDoEnvelope.UnidadeAdministradoraTipo);
        string? cidadeCodigoIbge = leitor.TextoOpcional(administradora, "cidadeCodigoIbge", path, LimitesDoEnvelope.UnidadeAdministradoraCidadeCodigoIbge);
        string? cidadeNome = leitor.TextoOpcional(administradora, "cidadeNome", path, LimitesDoEnvelope.UnidadeAdministradoraCidadeNome);
        string? cidadeUf = leitor.TextoOpcional(administradora, "cidadeUf", path, LimitesDoEnvelope.UnidadeAdministradoraCidadeUf);
        if (!leitor.Falhou)
        {
            ConferirCidade(leitor, cidadeCodigoIbge, cidadeNome, cidadeUf);
        }
    }

    private static void ConferirCidade(LeitorEnvelope leitor, string? codigoIbge, string? nome, string? uf)
    {
        bool algumPresente = codigoIbge is not null || nome is not null || uf is not null;
        bool todosPresentes = codigoIbge is not null && nome is not null && uf is not null;
        if (algumPresente && !todosPresentes)
        {
            leitor.Recusar<object>("'identidadesUnidade.administradora' tem cidade parcialmente preenchida — código IBGE, nome e UF têm de vir juntos ou nenhum.");
            return;
        }

        if (todosPresentes && ReferenciaCidadeGeo.Validar(codigoIbge, nome, uf) is { IsFailure: true } cidadeInvalida)
        {
            leitor.Propagar<object?>(cidadeInvalida.Error!);
        }
    }

    /// <summary>
    /// O rótulo e o tipo de campo de cada fato que as regras citam e nenhum formulário pergunta. O tipo é
    /// um dos que o formulário renderiza, ou nulo; o código, um só por item.
    /// </summary>
    internal static Dictionary<string, ApresentacaoDoFato> LerApresentacaoDosPressupostos(LeitorEnvelope leitor, JsonObject payload)
    {
        Dictionary<string, ApresentacaoDoFato> apresentacao = new(StringComparer.Ordinal);
        if (leitor.Falhou)
        {
            return apresentacao;
        }

        JsonArray array = leitor.Array(payload, "apresentacaoDosPressupostos", "$");
        for (int i = 0; !leitor.Falhou && i < array.Count; i++)
        {
            ApresentacaoDoFato? fato = LerApresentacao(
                leitor, leitor.ItemObjeto(array, i, "apresentacaoDosPressupostos"), $"apresentacaoDosPressupostos[{i}]");
            if (fato is null)
            {
                break;
            }

            if (!apresentacao.TryAdd(fato.FatoCodigo, fato))
            {
                leitor.Recusar<object>($"Envelope malformado em 'apresentacaoDosPressupostos': o fato '{fato.FatoCodigo}' aparece mais de uma vez.");
                break;
            }
        }

        return apresentacao;
    }

    private static ApresentacaoDoFato? LerApresentacao(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, "fatoCodigo", "rotulo", "tipoRenderizacao");
        string fatoCodigo = leitor.TextoNaoVazio(item, "fatoCodigo", path, FormaDoItem.FatoCodigoMaxLength);
        string rotulo = leitor.TextoNaoVazio(item, "rotulo", path, LimitesDoEnvelope.NomeDeCadastro);
        string? tipo = leitor.TextoOpcional(item, "tipoRenderizacao", path, LimitesDoEnvelope.Token);
        if (leitor.Falhou)
        {
            return null;
        }

        return tipo is not null && TipoRenderizacaoCodigo.FromCodigo(tipo) == TipoRenderizacao.Nenhuma
            ? leitor.Recusar<ApresentacaoDoFato?>($"Envelope malformado em '{path}.tipoRenderizacao': '{tipo}' não é um tipo de campo.")
            : new ApresentacaoDoFato(fatoCodigo, rotulo, tipo);
    }
}
