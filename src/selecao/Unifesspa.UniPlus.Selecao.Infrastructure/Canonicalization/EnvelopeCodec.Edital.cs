namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura do bloco de identidade do certame: dados do edital (<c>periodo</c>,
/// <c>hashesEdital</c>), o tipo de processo e a identidade da unidade administradora.
/// </summary>
public sealed partial class EnvelopeCodec
{
    /// <summary>
    /// Lê e fecha a forma do tipo autocontido. O agregado vivo já carrega a
    /// mesma cópia por valor e a recanonicalização posterior prova a paridade;
    /// o decoder não consulta Configuração para validar este dado histórico.
    /// </summary>
    private static void LerTipoProcesso(LeitorEnvelope leitor, JsonObject payload)
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

    private static DadosEdital? LerDadosEdital(LeitorEnvelope leitor, JsonObject payload, out string hashDocumento)
    {
        hashDocumento = string.Empty;

        JsonObject periodo = leitor.Objeto(payload, "periodo", "$");
        leitor.ExigirChaves(periodo, "periodo", "numero", "inicio", "fim");
        string? numero = leitor.TextoOpcional(periodo, "numero", "periodo", LimitesDoEnvelope.NumeroDoAto);
        DateTimeOffset inicio = leitor.Instante(periodo, "inicio", "periodo");
        DateTimeOffset fim = leitor.Instante(periodo, "fim", "periodo");

        JsonObject hashes = leitor.Objeto(payload, "hashesEdital", "$");
        leitor.ExigirChaves(hashes, "hashesEdital", "documentoEditalId", "hashSha256");
        Guid documentoId = leitor.Identificador(hashes, "documentoEditalId", "hashesEdital");
        string hash = leitor.Texto(hashes, "hashSha256", "hashesEdital");

        if (leitor.Falhou)
        {
            return null;
        }

        hashDocumento = hash;

        Result<DadosEdital> dados = DadosEdital.Criar(numero, inicio, fim, documentoId);
        return dados.IsFailure ? leitor.Propagar<DadosEdital>(dados.Error!) : dados.Value;
    }

    /// <summary>
    /// A identidade da Unidade administradora (issue #849, CA-04 da Feature #40): lida e
    /// validada para fechar a gramática, mas <b>não é propagada</b> — mesmo raciocínio de
    /// <c>origemCandidatos</c> em <see cref="LerCronogramaFases"/>. É atributo de raiz do
    /// agregado (<c>ProcessoSeletivo.UnidadeAdministradoraOrigemId</c>/<c>UnidadeAdministradora</c>),
    /// imutável após a criação (nenhum <c>Definir*</c> a altera, não há operação de re-bind) — a
    /// prova de fidelidade do round-trip vem de <c>ProcessoSeletivo.SombraParaVerificacao()</c>,
    /// que já copia os dois campos direto da raiz viva, nunca do envelope decodificado.
    /// </summary>
    /// <remarks>
    /// A cidade (issue #1114, ADR-0090) é opcional all-or-nothing: nula para processos
    /// congelados antes desta issue, trio completo e coerente para os demais. O decoder
    /// reconfirma a bicondicional e o formato via <see cref="ReferenciaCidadeGeo.Validar"/> —
    /// mesmo raciocínio de <see cref="LerDivulgacao"/>/<see cref="EnvelopeCodec.LerTaxaInscricao"/>:
    /// um trio parcial nunca sai do encoder (<c>UnidadeAdministradoraSnapshot</c> só existe com
    /// cidade completa ou nenhuma), então achar um é sinal de bytes que não vieram desse caminho.
    /// </remarks>
    private static void LerIdentidadesUnidade(LeitorEnvelope leitor, JsonObject payload)
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

        leitor.ExigirChaves(
            administradora, "identidadesUnidade.administradora",
            "origemId", "sigla", "slug", "nome", "tipo", "cidadeCodigoIbge", "cidadeNome", "cidadeUf");

        leitor.Identificador(administradora, "origemId", "identidadesUnidade.administradora");
        leitor.TextoNaoVazio(administradora, "sigla", "identidadesUnidade.administradora", LimitesDoEnvelope.UnidadeAdministradoraSigla);
        leitor.TextoNaoVazio(administradora, "slug", "identidadesUnidade.administradora", LimitesDoEnvelope.UnidadeAdministradoraSlug);
        leitor.TextoNaoVazio(administradora, "nome", "identidadesUnidade.administradora", LimitesDoEnvelope.UnidadeAdministradoraNome);
        leitor.TextoNaoVazio(administradora, "tipo", "identidadesUnidade.administradora", LimitesDoEnvelope.UnidadeAdministradoraTipo);
        string? cidadeCodigoIbge = leitor.TextoOpcional(administradora, "cidadeCodigoIbge", "identidadesUnidade.administradora", LimitesDoEnvelope.UnidadeAdministradoraCidadeCodigoIbge);
        string? cidadeNome = leitor.TextoOpcional(administradora, "cidadeNome", "identidadesUnidade.administradora", LimitesDoEnvelope.UnidadeAdministradoraCidadeNome);
        string? cidadeUf = leitor.TextoOpcional(administradora, "cidadeUf", "identidadesUnidade.administradora", LimitesDoEnvelope.UnidadeAdministradoraCidadeUf);
        if (leitor.Falhou)
        {
            return;
        }

        bool algumPresente = cidadeCodigoIbge is not null || cidadeNome is not null || cidadeUf is not null;
        bool todosPresentes = cidadeCodigoIbge is not null && cidadeNome is not null && cidadeUf is not null;
        if (algumPresente && !todosPresentes)
        {
            leitor.Propagar<object?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                "'identidadesUnidade.administradora' tem cidade parcialmente preenchida — código IBGE, nome e UF têm de vir juntos ou nenhum."));
            return;
        }

        if (todosPresentes)
        {
            Result cidadeValidacao = ReferenciaCidadeGeo.Validar(cidadeCodigoIbge, cidadeNome, cidadeUf);
            if (cidadeValidacao.IsFailure)
            {
                leitor.Propagar<object?>(cidadeValidacao.Error!);
            }
        }
    }
}
