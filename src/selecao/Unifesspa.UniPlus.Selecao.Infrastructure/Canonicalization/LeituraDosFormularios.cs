namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using LinhaDoPredicado = (int Clausula, string Fato, Unifesspa.UniPlus.Regras.Enums.Operador Operador, System.Text.Json.JsonElement Valor);

/// <summary>
/// Leitura do bloco de formulários de inscrição: etapas, termos exigidos e modelo de origem.
/// </summary>
internal static class LeituraDosFormularios
{
    /// <summary>
    /// Os formulários por finalidade (UNI-REQ-0144), em ordem de finalidade e sem repetição, cada um
    /// remontado pelas factories do domínio — que reconferem a estrutura das etapas — com os seus
    /// termos, em ordem crescente e com código único no formulário.
    /// </summary>
    internal static (IReadOnlyList<FormularioProcesso> Formularios, IReadOnlyList<TermoExigidoFormulario> Termos) LerFormularios(
        LeitorEnvelope leitor, JsonObject payload)
    {
        JsonArray blocos = leitor.Array(payload, "formularios", "$");
        if (leitor.Falhou)
        {
            return ([], []);
        }

        List<FormularioProcesso> formularios = [];
        List<TermoExigidoFormulario> termos = [];
        for (int f = 0; f < blocos.Count; f++)
        {
            FormularioProcesso? formulario = LerFormulario(
                leitor, leitor.ItemObjeto(blocos, f, "formularios"), $"formularios[{f}]", formularios.LastOrDefault()?.Finalidade, termos);
            if (formulario is null)
            {
                return ([], []);
            }

            formularios.Add(formulario);
        }

        return (formularios, termos);
    }

    private static FormularioProcesso? LerFormulario(
        LeitorEnvelope leitor, JsonObject bloco, string path, FinalidadeFormulario? finalidadeAnterior, List<TermoExigidoFormulario> termos)
    {
        leitor.ExigirChaves(bloco, path, "finalidade", "faseId", "titulo", "modeloOrigem", "etapas", "termos");

        FinalidadeFormulario finalidade = EstruturaFormulario.FinalidadeDoToken(leitor.TextoNaoVazio(bloco, "finalidade", path));
        Guid? faseId = leitor.IdentificadorOpcional(bloco, "faseId", path);
        string? titulo = leitor.TextoOpcional(bloco, "titulo", path, LimitesDoEnvelope.NomeDeCadastro);
        (Guid? modeloId, string? modeloCodigo) = LerModeloDeOrigem(leitor, bloco, path);
        IReadOnlyList<EtapaFormulario> etapas = LerEtapasDoFormulario(leitor, bloco, path);
        JsonArray itensDeTermo = leitor.Array(bloco, "termos", path);
        if (leitor.Falhou)
        {
            return null;
        }

        // O encoder emite as finalidades em ordem estritamente crescente, uma vez cada.
        if (finalidade == FinalidadeFormulario.Nenhuma || finalidade <= finalidadeAnterior)
        {
            return leitor.Recusar<FormularioProcesso?>($"'{path}.finalidade' fora do vocabulário, repetida ou fora de ordem.");
        }

        Result<FormularioProcesso> formulario = FormularioProcesso.Criar(finalidade, faseId, titulo, etapas, modeloId, modeloCodigo);
        if (formulario.IsFailure)
        {
            return leitor.Propagar<FormularioProcesso?>(formulario.Error!);
        }

        return LerTermos(leitor, itensDeTermo, path, finalidade, termos) ? formulario.Value : null;
    }

    private static bool LerTermos(
        LeitorEnvelope leitor, JsonArray itensDeTermo, string path, FinalidadeFormulario finalidade, List<TermoExigidoFormulario> termos)
    {
        HashSet<string> codigos = new(StringComparer.Ordinal);
        int? ordemAnterior = null;
        for (int i = 0; i < itensDeTermo.Count; i++)
        {
            string pathTermo = $"{path}.termos[{i}]";
            TermoExigidoFormulario? termo = LerTermoExigido(leitor, leitor.ItemObjeto(itensDeTermo, i, $"{path}.termos"), pathTermo, finalidade);
            if (leitor.Falhou || termo is null)
            {
                return false;
            }

            // O encoder emite os termos em ordem estritamente crescente e com código único; outra forma
            // só vem de envelope adulterado.
            if (!codigos.Add(termo.Codigo) || termo.Ordem <= ordemAnterior)
            {
                leitor.Recusar<object>($"'{pathTermo}' repete código ou quebra a ordem crescente dos termos.");
                return false;
            }

            ordemAnterior = termo.Ordem;
            termos.Add(termo);
        }

        return true;
    }

    internal static (Guid? Id, string? Codigo) LerModeloDeOrigem(LeitorEnvelope leitor, JsonObject bloco, string path)
    {
        if (leitor.ObjetoOpcional(bloco, "modeloOrigem", path) is not { } modelo)
        {
            return (null, null);
        }

        string pathModelo = $"{path}.modeloOrigem";
        leitor.ExigirChaves(modelo, pathModelo, "id", "codigo");
        return (leitor.Identificador(modelo, "id", pathModelo), leitor.TextoOpcional(modelo, "codigo", pathModelo, LimitesDoEnvelope.CodigoModeloDeFormulario));
    }

    internal static IReadOnlyList<EtapaFormulario> LerEtapasDoFormulario(LeitorEnvelope leitor, JsonObject bloco, string path)
    {
        JsonArray itens = leitor.Array(bloco, "etapas", path);
        if (leitor.Falhou)
        {
            return [];
        }

        List<EtapaFormulario> etapas = [];
        for (int i = 0; i < itens.Count; i++)
        {
            EtapaFormulario? etapa = LerEtapaDoFormulario(
                leitor, leitor.ItemObjeto(itens, i, $"{path}.etapas"), $"{path}.etapas[{i}]", etapas.LastOrDefault()?.Ordem);
            if (etapa is null)
            {
                return [];
            }

            etapas.Add(etapa);
        }

        return etapas;
    }

    private static EtapaFormulario? LerEtapaDoFormulario(LeitorEnvelope leitor, JsonObject item, string pathEtapa, int? ordemAnterior)
    {
        leitor.ExigirChaves(item, pathEtapa, "codigo", "ordem", "tipo", "bloco", "titulo", "descricao", "aviso", "exibicao");
        string codigo = leitor.TextoNaoVazio(item, "codigo", pathEtapa, LimitesDoEnvelope.CodigoEtapaFormulario);
        int ordem = leitor.Inteiro(item, "ordem", pathEtapa);
        string tipo = leitor.TextoNaoVazio(item, "tipo", pathEtapa);
        string? blocoDeSistema = leitor.TextoOpcional(item, "bloco", pathEtapa);
        string titulo = leitor.TextoNaoVazio(item, "titulo", pathEtapa, LimitesDoEnvelope.TituloEtapaFormulario);
        string? descricao = leitor.TextoOpcional(item, "descricao", pathEtapa, LimitesDoEnvelope.TextoEtapaFormulario);
        string? aviso = leitor.TextoOpcional(item, "aviso", pathEtapa, LimitesDoEnvelope.TextoEtapaFormulario);
        IReadOnlyList<LinhaDoPredicado> exibicao = LeituraDasCondicoes.LerDnf(leitor, item, "exibicao", pathEtapa);
        if (leitor.Falhou)
        {
            return null;
        }

        Result<PredicadoDnf?> exibicaoLida = LeituraDasCondicoes.PredicadoOpcional(exibicao);
        if (exibicaoLida.IsFailure)
        {
            return leitor.Propagar<EtapaFormulario?>(exibicaoLida.Error!);
        }

        // O encoder emite as etapas em ordem estritamente crescente; outra forma só vem de envelope
        // adulterado e não reproduziria os bytes.
        if (ordem <= ordemAnterior)
        {
            return leitor.Recusar<EtapaFormulario?>($"'{pathEtapa}' quebra a ordem crescente das etapas.");
        }

        Result<EtapaFormulario> etapa = EtapaFormulario.Criar(
            codigo, ordem, EstruturaFormulario.TipoDoToken(tipo), EstruturaFormulario.BlocoDoToken(blocoDeSistema), titulo, descricao, aviso,
            exibicaoLida.Value);
        return etapa.IsFailure ? leitor.Propagar<EtapaFormulario?>(etapa.Error!) : etapa.Value;
    }

    internal static TermoExigidoFormulario? LerTermoExigido(
        LeitorEnvelope leitor, JsonObject item, string path, FinalidadeFormulario finalidade)
    {
        leitor.ExigirChaves(item, path, "codigo", "ordem", "termoId", "versaoId", "nome", "texto", "baseLegal",
            "formaAceite", "hashVersao", "exibicao", "obrigatoriedade");

        string codigo = leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.CodigoTermoExigido);
        int ordem = leitor.Inteiro(item, "ordem", path);
        VersaoTermoEscolhida versao = LerVersaoDoTermo(leitor, item, path);
        IReadOnlyList<LinhaDoPredicado> exibicao = LeituraDasCondicoes.LerDnf(leitor, item, "exibicao", path);
        if (leitor.Falhou)
        {
            return null;
        }

        Result<PredicadoDnf?> exibicaoLida = LeituraDasCondicoes.PredicadoOpcional(exibicao);
        if (exibicaoLida.IsFailure)
        {
            return leitor.Propagar<TermoExigidoFormulario>(exibicaoLida.Error!);
        }

        if (LeituraDasCondicoes.LerObrigatoriedade(leitor, item, path) is not { } obrigatoriedade)
        {
            return null;
        }

        Result<TermoExigidoFormulario> termo = TermoExigidoFormulario.Criar(
            codigo, ordem, versao, exibicaoLida.Value, obrigatoriedade, finalidade);
        return termo.IsSuccess ? termo.Value : leitor.Propagar<TermoExigidoFormulario>(termo.Error!);
    }

    private static VersaoTermoEscolhida LerVersaoDoTermo(LeitorEnvelope leitor, JsonObject item, string path) => new(
        leitor.Identificador(item, "termoId", path),
        leitor.Identificador(item, "versaoId", path),
        leitor.TextoNaoVazio(item, "nome", path, LimitesDoEnvelope.NomeDoTermo),
        leitor.TextoNaoVazio(item, "texto", path, LimitesDoEnvelope.TextoDoTermo),
        leitor.TextoNaoVazio(item, "baseLegal", path, LimitesDoEnvelope.BaseLegalDoTermo),
        leitor.TextoNaoVazio(item, "formaAceite", path, LimitesDoEnvelope.FormaAceiteDoTermo),
        leitor.TextoNaoVazio(item, "hashVersao", path, LimitesDoEnvelope.HashDaVersaoDoTermo));
}
