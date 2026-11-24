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

        return LerBlocosDeFormulario(leitor, blocos);
    }

    private static (IReadOnlyList<FormularioProcesso> Formularios, IReadOnlyList<TermoExigidoFormulario> Termos) LerBlocosDeFormulario(
        LeitorEnvelope leitor, JsonArray blocos)
    {
        List<FormularioProcesso> formularios = [];
        List<TermoExigidoFormulario> termos = [];
        for (int f = 0; f < blocos.Count; f++)
        {
            FormularioProcesso? formulario = LerItemDeFormulario(leitor, blocos, f, formularios.LastOrDefault()?.Finalidade, termos);
            if (formulario is null)
            {
                return ([], []);
            }

            formularios.Add(formulario);
        }

        return (formularios, termos);
    }

    private static FormularioProcesso? LerItemDeFormulario(
        LeitorEnvelope leitor, JsonArray blocos, int indice, FinalidadeFormulario? anterior, List<TermoExigidoFormulario> termos) =>
        LerFormulario(leitor, leitor.ItemObjeto(blocos, indice, "formularios"), $"formularios[{indice}]", anterior, termos);

    private static FormularioProcesso? LerFormulario(
        LeitorEnvelope leitor, JsonObject bloco, string path, FinalidadeFormulario? finalidadeAnterior, List<TermoExigidoFormulario> termos)
    {
        leitor.ExigirChaves(bloco, path, "finalidade", "faseId", "titulo", "modeloOrigem", "paginas", "termos");

        return LerDeclaracaoDoFormulario(leitor, bloco, path, finalidadeAnterior, termos);
    }

    private static FormularioProcesso? LerDeclaracaoDoFormulario(
        LeitorEnvelope leitor, JsonObject bloco, string path, FinalidadeFormulario? finalidadeAnterior, List<TermoExigidoFormulario> termos)
    {
        FinalidadeFormulario finalidade = EstruturaFormulario.FinalidadeDoToken(leitor.TextoNaoVazio(bloco, "finalidade", path));
        Guid? faseId = leitor.IdentificadorOpcional(bloco, "faseId", path);
        string? titulo = leitor.TextoOpcional(bloco, "titulo", path, LimitesDoEnvelope.NomeDeCadastro);
        (Guid? modeloId, string? modeloCodigo, string? modeloNome) = LerModeloDeOrigem(leitor, bloco, path);
        IReadOnlyList<EtapaFormulario> etapas = LerEtapasDoFormulario(leitor, bloco, path);
        JsonArray itensDeTermo = leitor.Array(bloco, "termos", path);
        if (leitor.Falhou)
        {
            return null;
        }

        return CriarFormulario(leitor, path, finalidadeAnterior, termos,
            new DeclaracaoFormulario(finalidade, faseId, titulo, modeloId, modeloCodigo, modeloNome, etapas, itensDeTermo));
    }

    private sealed record DeclaracaoFormulario(
        FinalidadeFormulario Finalidade, Guid? FaseId, string? Titulo, Guid? ModeloId, string? ModeloCodigo, string? ModeloNome,
        IReadOnlyList<EtapaFormulario> Etapas, JsonArray Termos);

    private static FormularioProcesso? CriarFormulario(
        LeitorEnvelope leitor, string path, FinalidadeFormulario? finalidadeAnterior, List<TermoExigidoFormulario> termos, DeclaracaoFormulario d)
    {
        if (d.Finalidade == FinalidadeFormulario.Nenhuma || d.Finalidade <= finalidadeAnterior)
        {
            return leitor.Recusar<FormularioProcesso?>($"'{path}.finalidade' fora do vocabulário, repetida ou fora de ordem.");
        }

        Result<FormularioProcesso> formulario = FormularioProcesso.Criar(d.Finalidade, d.FaseId, d.Titulo, d.Etapas, d.ModeloId, d.ModeloCodigo, d.ModeloNome);
        if (formulario.IsFailure)
        {
            return leitor.Propagar<FormularioProcesso?>(formulario.Error!);
        }

        return LerTermos(leitor, d.Termos, path, d.Finalidade, termos) ? formulario.Value : null;
    }

    private static bool LerTermos(
        LeitorEnvelope leitor, JsonArray itensDeTermo, string path, FinalidadeFormulario finalidade, List<TermoExigidoFormulario> termos)
    {
        HashSet<string> codigos = new(StringComparer.Ordinal);
        int? ordemAnterior = null;
        for (int i = 0; i < itensDeTermo.Count; i++)
        {
            if (!AdicionarTermo(leitor, itensDeTermo, (path, i), finalidade, termos, (codigos, ordemAnterior), out ordemAnterior))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AdicionarTermo(
        LeitorEnvelope leitor, JsonArray itens, (string Path, int Indice) posicao, FinalidadeFormulario finalidade, List<TermoExigidoFormulario> termos,
        (HashSet<string> Codigos, int? OrdemAnterior) conferencia, out int? ordemAnterior)
    {
        ordemAnterior = conferencia.OrdemAnterior;
        TermoExigidoFormulario? termo = LerItemDeTermo(leitor, itens, posicao, finalidade);
        if (termo is null)
        {
            return false;
        }

        if (!ConferirTermo(leitor, $"{posicao.Path}.termos[{posicao.Indice}]", termo, conferencia))
        {
            return false;
        }

        return RegistrarTermo(termos, termo, out ordemAnterior);
    }

    private static bool RegistrarTermo(List<TermoExigidoFormulario> termos, TermoExigidoFormulario termo, out int? ordemAnterior)
    {
        ordemAnterior = termo.Ordem;
        termos.Add(termo);
        return true;
    }

    private static TermoExigidoFormulario? LerItemDeTermo(
        LeitorEnvelope leitor, JsonArray itens, (string Path, int Indice) posicao, FinalidadeFormulario finalidade)
    {
        TermoExigidoFormulario? termo = LerTermoExigido(leitor, leitor.ItemObjeto(itens, posicao.Indice, $"{posicao.Path}.termos"),
            $"{posicao.Path}.termos[{posicao.Indice}]", finalidade);
        return leitor.Falhou ? null : termo;
    }

    private static bool ConferirTermo(LeitorEnvelope leitor, string path, TermoExigidoFormulario termo,
        (HashSet<string> Codigos, int? OrdemAnterior) conferencia)
    {
        if (!conferencia.Codigos.Add(termo.Codigo) || termo.Ordem <= conferencia.OrdemAnterior)
        {
            leitor.Recusar<object>($"'{path}' repete código ou quebra a ordem crescente dos termos.");
            return false;
        }

        return true;
    }

    internal static (Guid? Id, string? Codigo, string? Nome) LerModeloDeOrigem(LeitorEnvelope leitor, JsonObject bloco, string path)
    {
        if (leitor.ObjetoOpcional(bloco, "modeloOrigem", path) is not { } modelo)
        {
            return (null, null, null);
        }

        string pathModelo = $"{path}.modeloOrigem";
        leitor.ExigirChaves(modelo, pathModelo, "id", "codigo", "nome");
        Guid id = leitor.Identificador(modelo, "id", pathModelo);
        string? codigo = leitor.TextoOpcional(modelo, "codigo", pathModelo, LimitesDoEnvelope.CodigoModeloDeFormulario);
        string? nome = leitor.TextoOpcional(modelo, "nome", pathModelo, LimitesDoEnvelope.NomeModeloDeFormulario);
        return !leitor.Falhou && !OrigemCoerente(codigo, nome)
            ? leitor.Recusar<(Guid?, string?, string?)>($"'{pathModelo}' exige código e nome preenchidos ou ambos nulos.")
            : (id, codigo, nome);
    }

    private static bool OrigemCoerente(string? codigo, string? nome) =>
        (codigo is null && nome is null) || (!string.IsNullOrWhiteSpace(codigo) && !string.IsNullOrWhiteSpace(nome));

    internal static IReadOnlyList<EtapaFormulario> LerEtapasDoFormulario(LeitorEnvelope leitor, JsonObject bloco, string path)
    {
        JsonArray itens = leitor.Array(bloco, "paginas", path);
        if (leitor.Falhou)
        {
            return [];
        }

        return LerPaginas(leitor, itens, path);
    }

    private static List<EtapaFormulario> LerPaginas(LeitorEnvelope leitor, JsonArray itens, string path)
    {
        List<EtapaFormulario> etapas = [];
        for (int i = 0; i < itens.Count; i++)
        {
            EtapaFormulario? etapa = LerEtapaDoFormulario(
                leitor, leitor.ItemObjeto(itens, i, $"{path}.paginas"), $"{path}.paginas[{i}]", etapas.LastOrDefault()?.Ordem);
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
        DeclaracaoEtapa d = LerDeclaracaoDaEtapa(leitor, item, pathEtapa);
        if (leitor.Falhou)
        {
            return null;
        }

        return CriarEtapa(leitor, pathEtapa, ordemAnterior, d);
    }

    private sealed record DeclaracaoEtapa(string Codigo, int Ordem, string Tipo, string? Bloco, string Titulo, string? Descricao,
        string? Aviso, IReadOnlyList<LinhaDoPredicado> Exibicao);

    private static DeclaracaoEtapa LerDeclaracaoDaEtapa(LeitorEnvelope leitor, JsonObject item, string path) => new(
        leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.CodigoEtapaFormulario),
        leitor.Inteiro(item, "ordem", path),
        leitor.TextoNaoVazio(item, "tipo", path),
        leitor.TextoOpcional(item, "bloco", path),
        leitor.TextoNaoVazio(item, "titulo", path, LimitesDoEnvelope.TituloEtapaFormulario),
        leitor.TextoOpcional(item, "descricao", path, LimitesDoEnvelope.TextoEtapaFormulario),
        leitor.TextoOpcional(item, "aviso", path, LimitesDoEnvelope.TextoEtapaFormulario),
        LeituraDasCondicoes.LerDnf(leitor, item, "exibicao", path));

    private static EtapaFormulario? CriarEtapa(LeitorEnvelope leitor, string pathEtapa, int? ordemAnterior, DeclaracaoEtapa d)
    {
        Result<PredicadoDnf?> exibicaoLida = LeituraDasCondicoes.PredicadoOpcional(d.Exibicao);
        if (exibicaoLida.IsFailure)
        {
            return leitor.Propagar<EtapaFormulario?>(exibicaoLida.Error!);
        }

        return MontarEtapa(leitor, pathEtapa, ordemAnterior, d, exibicaoLida.Value);
    }

    private static EtapaFormulario? MontarEtapa(
        LeitorEnvelope leitor, string pathEtapa, int? ordemAnterior, DeclaracaoEtapa d, PredicadoDnf? exibicao)
    {
        // O encoder emite as etapas em ordem estritamente crescente; outra forma só vem de envelope
        // adulterado e não reproduziria os bytes.
        if (d.Ordem <= ordemAnterior)
        {
            return leitor.Recusar<EtapaFormulario?>($"'{pathEtapa}' quebra a ordem crescente das etapas.");
        }

        Result<EtapaFormulario> etapa = EtapaFormulario.Criar(
            d.Codigo, d.Ordem, EstruturaFormulario.TipoDoToken(d.Tipo), EstruturaFormulario.BlocoDoToken(d.Bloco), d.Titulo, d.Descricao, d.Aviso,
            exibicao);
        return etapa.IsFailure ? leitor.Propagar<EtapaFormulario?>(etapa.Error!) : etapa.Value;
    }

    internal static TermoExigidoFormulario? LerTermoExigido(
        LeitorEnvelope leitor, JsonObject item, string path, FinalidadeFormulario finalidade)
    {
        leitor.ExigirChaves(item, path, "codigo", "ordem", "termoId", "versaoId", "termoCodigo", "versaoNumero", "nome", "texto", "baseLegal",
            "formaAceite", "hashVersao", "exibicao", "obrigatoriedade");

        string codigo = leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.CodigoTermoExigido);
        int ordem = leitor.Inteiro(item, "ordem", path);
        VersaoTermoEscolhida versao = LerVersaoDoTermo(leitor, item, path);
        IReadOnlyList<LinhaDoPredicado> exibicao = LeituraDasCondicoes.LerDnf(leitor, item, "exibicao", path);
        if (leitor.Falhou)
        {
            return null;
        }

        return CriarTermo(leitor, item, path, finalidade, (codigo, ordem, versao, exibicao));
    }

    private static TermoExigidoFormulario? CriarTermo(
        LeitorEnvelope leitor, JsonObject item, string path, FinalidadeFormulario finalidade,
        (string Codigo, int Ordem, VersaoTermoEscolhida Versao, IReadOnlyList<LinhaDoPredicado> Exibicao) declaracao)
    {
        Result<PredicadoDnf?> exibicaoLida = LeituraDasCondicoes.PredicadoOpcional(declaracao.Exibicao);
        if (exibicaoLida.IsFailure)
        {
            return leitor.Propagar<TermoExigidoFormulario>(exibicaoLida.Error!);
        }

        if (LeituraDasCondicoes.LerObrigatoriedade(leitor, item, path) is not { } obrigatoriedade)
        {
            return null;
        }

        Result<TermoExigidoFormulario> termo = TermoExigidoFormulario.Criar(
            declaracao.Codigo, declaracao.Ordem, declaracao.Versao, exibicaoLida.Value, obrigatoriedade, finalidade);
        return termo.IsSuccess ? termo.Value : leitor.Propagar<TermoExigidoFormulario>(termo.Error!);
    }

    private static int LerNumeroDaVersao(LeitorEnvelope leitor, JsonObject item, string path)
    {
        int numero = leitor.Inteiro(item, "versaoNumero", path);
        return numero < 1 && !leitor.Falhou
            ? leitor.Malformado<int>($"{path}.versaoNumero", "esperado um inteiro a partir de 1.")
            : numero;
    }

    private static VersaoTermoEscolhida LerVersaoDoTermo(LeitorEnvelope leitor, JsonObject item, string path) => new(
        leitor.Identificador(item, "termoId", path),
        leitor.Identificador(item, "versaoId", path),
        leitor.TextoNaoVazio(item, "termoCodigo", path, LimitesDoEnvelope.CodigoDoTermoNoCatalogo),
        LerNumeroDaVersao(leitor, item, path),
        leitor.TextoNaoVazio(item, "nome", path, LimitesDoEnvelope.NomeDoTermo),
        leitor.TextoNaoVazio(item, "texto", path, LimitesDoEnvelope.TextoDoTermo),
        leitor.TextoNaoVazio(item, "baseLegal", path, LimitesDoEnvelope.BaseLegalDoTermo),
        leitor.CodigoDeContrato<FormaAceite>(item, "formaAceite", path),
        leitor.TextoNaoVazio(item, "hashVersao", path, LimitesDoEnvelope.HashDaVersaoDoTermo));
}
