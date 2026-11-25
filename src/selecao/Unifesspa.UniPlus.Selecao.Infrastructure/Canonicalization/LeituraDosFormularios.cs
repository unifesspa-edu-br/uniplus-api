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
/// Leitura do bloco de formulários de inscrição: páginas, termos exigidos e modelo de origem.
/// </summary>
internal static class LeituraDosFormularios
{
    /// <summary>
    /// Os formulários por finalidade (UNI-REQ-0144), em ordem de finalidade e sem repetição, cada um
    /// remontado pelas factories do domínio — que reconferem a estrutura das páginas — com os seus
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
        List<FormularioComTermosExigidos> formularios = [];
        for (int indice = 0; indice < blocos.Count; indice++)
        {
            FormularioComTermosExigidos? formulario = LerFormulario(leitor, leitor.ItemObjeto(blocos, indice, "formularios"),
                $"formularios[{indice}]", formularios.LastOrDefault()?.Formulario.Finalidade);
            if (formulario is null)
            {
                return ([], []);
            }

            formularios.Add(formulario);
        }

        return (formularios.Select(formulario => formulario.Formulario).ToList(), formularios.SelectMany(formulario => formulario.Termos).ToList());
    }

    private sealed record FormularioComTermosExigidos(FormularioProcesso Formulario, IReadOnlyList<TermoExigidoFormulario> Termos);

    private static FormularioComTermosExigidos? LerFormulario(
        LeitorEnvelope leitor, JsonObject bloco, string path, FinalidadeFormulario? finalidadeAnterior)
    {
        leitor.ExigirChaves(bloco, path, "finalidade", "faseId", "titulo", "modeloOrigem", "paginas", "termos");
        DeclaracaoFormulario declaracao = LerDeclaracaoDoFormulario(leitor, bloco, path);
        if (leitor.Falhou)
        {
            return null;
        }

        return CriarFormulario(leitor, path, finalidadeAnterior, declaracao);
    }

    private static DeclaracaoFormulario LerDeclaracaoDoFormulario(LeitorEnvelope leitor, JsonObject bloco, string path)
    {
        FinalidadeFormulario? finalidade = EstruturaFormulario.FinalidadeDoCodigo(leitor.TextoNaoVazio(bloco, "finalidade", path));
        Guid? faseId = leitor.IdentificadorOpcional(bloco, "faseId", path);
        string? titulo = leitor.TextoOpcional(bloco, "titulo", path, LimitesDoEnvelope.NomeDeCadastro);
        (Guid? modeloId, string? modeloCodigo, string? modeloNome) = LerModeloDeOrigem(leitor, bloco, path);
        List<PaginaDoFormulario> paginas = LerPaginasDoFormulario(leitor, bloco, path);
        JsonArray itensDeTermo = leitor.Array(bloco, "termos", path);
        return new DeclaracaoFormulario(finalidade, faseId, titulo, modeloId, modeloCodigo, modeloNome, paginas, itensDeTermo);
    }

    private sealed record DeclaracaoFormulario(
        FinalidadeFormulario? Finalidade, Guid? FaseId, string? Titulo, Guid? ModeloId, string? ModeloCodigo, string? ModeloNome,
        IReadOnlyList<PaginaDoFormulario> Paginas, JsonArray Termos);

    private static FormularioComTermosExigidos? CriarFormulario(
        LeitorEnvelope leitor, string path, FinalidadeFormulario? finalidadeAnterior, DeclaracaoFormulario declaracao)
    {
        if (declaracao.Finalidade is not { } finalidade || finalidade <= finalidadeAnterior)
        {
            return leitor.Recusar<FormularioComTermosExigidos?>($"'{path}.finalidade' fora do vocabulário, repetida ou fora de ordem.");
        }
        Result<FormularioProcesso> formulario = FormularioProcesso.Criar(declaracao.Finalidade, declaracao.FaseId, declaracao.Titulo,
            declaracao.Paginas, declaracao.ModeloId, declaracao.ModeloCodigo, declaracao.ModeloNome);
        if (formulario.IsFailure)
        {
            return leitor.Propagar<FormularioComTermosExigidos?>(formulario.Error!);
        }
        List<TermoExigidoFormulario> termos = LerTermos(leitor, declaracao.Termos, path, finalidade);
        return leitor.Falhou ? null : new FormularioComTermosExigidos(formulario.Value!, termos);
    }

    private static List<TermoExigidoFormulario> LerTermos(
        LeitorEnvelope leitor, JsonArray itens, string path, FinalidadeFormulario finalidade)
    {
        List<TermoExigidoFormulario> termos = [];
        for (int indice = 0; indice < itens.Count; indice++)
        {
            TermoExigidoFormulario? termo = LerTermoSeguinte(leitor, leitor.ItemObjeto(itens, indice, $"{path}.termos"),
                $"{path}.termos[{indice}]", finalidade, termos);
            if (termo is null)
            {
                return [];
            }

            termos.Add(termo);
        }

        return termos;
    }

    private static TermoExigidoFormulario? LerTermoSeguinte(
        LeitorEnvelope leitor, JsonObject item, string pathTermo, FinalidadeFormulario finalidade, IReadOnlyList<TermoExigidoFormulario> anteriores)
    {
        TermoExigidoFormulario? termo = LerTermoExigido(leitor, item, pathTermo, finalidade);
        return termo is null || SegueOsTermosAnteriores(termo, anteriores)
            ? termo
            : leitor.Recusar<TermoExigidoFormulario?>($"'{pathTermo}' repete código ou quebra a ordem crescente dos termos.");
    }

    private static bool SegueOsTermosAnteriores(TermoExigidoFormulario termo, IReadOnlyList<TermoExigidoFormulario> anteriores) =>
        (anteriores.Count == 0 || termo.Ordem > anteriores[^1].Ordem)
        && !anteriores.Any(anterior => string.Equals(anterior.Codigo, termo.Codigo, StringComparison.Ordinal));

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

    private static List<PaginaDoFormulario> LerPaginasDoFormulario(LeitorEnvelope leitor, JsonObject bloco, string path)
    {
        JsonArray itens = leitor.Array(bloco, "paginas", path);
        if (leitor.Falhou)
        {
            return [];
        }

        return LerPaginas(leitor, itens, path);
    }

    private static List<PaginaDoFormulario> LerPaginas(LeitorEnvelope leitor, JsonArray itens, string path)
    {
        List<PaginaDoFormulario> paginas = [];
        for (int i = 0; i < itens.Count; i++)
        {
            PaginaDoFormulario? pagina = LerPaginaDoFormulario(
                leitor, leitor.ItemObjeto(itens, i, $"{path}.paginas"), $"{path}.paginas[{i}]", paginas.LastOrDefault()?.Ordem);
            if (pagina is null)
            {
                return [];
            }

            paginas.Add(pagina);
        }

        return paginas;
    }

    private static PaginaDoFormulario? LerPaginaDoFormulario(LeitorEnvelope leitor, JsonObject item, string pathPagina, int? ordemAnterior)
    {
        if (leitor.Falhou)
        {
            return null;
        }

        leitor.ExigirChaves(item, pathPagina, "codigo", "ordem", "tipo", "bloco", "titulo", "descricao", "aviso", "exibicao");
        DeclaracaoPagina d = LerDeclaracaoDaPagina(leitor, item, pathPagina);
        if (leitor.Falhou)
        {
            return null;
        }

        return CriarPagina(leitor, pathPagina, ordemAnterior, d);
    }

    private sealed record DeclaracaoPagina(string Codigo, int Ordem, TipoPaginaFormulario Tipo, BlocoSistema? Bloco, string Titulo, string? Descricao,
        string? Aviso, IReadOnlyList<LinhaDoPredicado> Exibicao);

    private static DeclaracaoPagina LerDeclaracaoDaPagina(LeitorEnvelope leitor, JsonObject item, string path) => new(
        leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.CodigoPaginaFormulario),
        leitor.Inteiro(item, "ordem", path),
        leitor.CodigoDeContrato<TipoPaginaFormulario>(item, "tipo", path),
        leitor.CodigoDeContratoOpcional<BlocoSistema>(item, "bloco", path),
        leitor.TextoNaoVazio(item, "titulo", path, LimitesDoEnvelope.TituloPaginaFormulario),
        leitor.TextoOpcional(item, "descricao", path, LimitesDoEnvelope.TextoPaginaFormulario),
        leitor.TextoOpcional(item, "aviso", path, LimitesDoEnvelope.TextoPaginaFormulario),
        LeituraDasCondicoes.LerDnf(leitor, item, "exibicao", path));

    private static PaginaDoFormulario? CriarPagina(LeitorEnvelope leitor, string pathPagina, int? ordemAnterior, DeclaracaoPagina d)
    {
        Result<PredicadoDnf?> exibicaoLida = LeituraDasCondicoes.PredicadoOpcional(d.Exibicao);
        if (exibicaoLida.IsFailure)
        {
            return leitor.Propagar<PaginaDoFormulario?>(exibicaoLida.Error!);
        }

        return MontarPagina(leitor, pathPagina, ordemAnterior, d, exibicaoLida.Value);
    }

    private static PaginaDoFormulario? MontarPagina(
        LeitorEnvelope leitor, string pathPagina, int? ordemAnterior, DeclaracaoPagina d, PredicadoDnf? exibicao)
    {
        // O encoder emite as páginas em ordem estritamente crescente; outra forma só vem de envelope
        // adulterado e não reproduziria os bytes.
        if (d.Ordem <= ordemAnterior)
        {
            return leitor.Recusar<PaginaDoFormulario?>($"'{pathPagina}' quebra a ordem crescente das páginas.");
        }

        Result<PaginaDoFormulario> pagina = PaginaDoFormulario.Criar(
            new PaginaEstrutura(d.Codigo, d.Ordem, d.Tipo, d.Bloco),
            d.Titulo, d.Descricao, d.Aviso, exibicao);
        return pagina.IsFailure ? leitor.Propagar<PaginaDoFormulario?>(pagina.Error!) : pagina.Value;
    }

    private static TermoExigidoFormulario? LerTermoExigido(
        LeitorEnvelope leitor, JsonObject item, string path, FinalidadeFormulario finalidade)
    {
        if (leitor.Falhou)
        {
            return null;
        }

        leitor.ExigirChaves(item, path, "codigo", "ordem", "termoId", "versaoId", "termoCodigo", "versaoNumero", "nome", "texto", "baseLegal",
            "formaAceite", "hashVersao", "exibicao", "obrigatoriedade");

        DeclaracaoTermo declaracao = LerDeclaracaoDoTermo(leitor, item, path);
        return leitor.Falhou ? null : CriarTermo(leitor, item, path, finalidade, declaracao);
    }

    private sealed record DeclaracaoTermo(string Codigo, int Ordem, VersaoTermoEscolhida Versao, IReadOnlyList<LinhaDoPredicado> Exibicao);

    private static DeclaracaoTermo LerDeclaracaoDoTermo(LeitorEnvelope leitor, JsonObject item, string path) => new(
        leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.CodigoTermoExigido),
        leitor.Inteiro(item, "ordem", path),
        LerVersaoDoTermo(leitor, item, path),
        LeituraDasCondicoes.LerDnf(leitor, item, "exibicao", path));

    private static TermoExigidoFormulario? CriarTermo(
        LeitorEnvelope leitor, JsonObject item, string path, FinalidadeFormulario finalidade, DeclaracaoTermo declaracao)
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
