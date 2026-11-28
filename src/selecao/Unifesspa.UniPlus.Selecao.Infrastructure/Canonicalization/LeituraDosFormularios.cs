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
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using LinhaDoPredicado = (int Clausula, string Fato, Unifesspa.UniPlus.Regras.Enums.Operador Operador, System.Text.Json.JsonElement Valor);

/// <summary>
/// Leitura do bloco de formulários do processo: código, obrigatoriedade, páginas, termos exigidos e modelo de origem.
/// </summary>
internal static class LeituraDosFormularios
{
    /// <summary>
    /// Os formulários do processo (UNI-REQ-0144), pela ordem ordinal do código e sem repetição, cada um
    /// remontado pelas factories do domínio — que reconferem a estrutura das páginas contra as fases que o
    /// documento congelou — com os seus termos, em ordem crescente e com código único no formulário.
    /// </summary>
    internal static (IReadOnlyList<FormularioProcesso> Formularios, IReadOnlyList<TermoExigidoFormulario> Termos) LerFormularios(
        LeitorEnvelope leitor, JsonObject payload, CronogramaDoProcesso cronograma)
    {
        JsonArray blocos = leitor.Array(payload, "formularios", "$");
        if (leitor.Falhou)
        {
            return ([], []);
        }

        return LerBlocosDeFormulario(leitor, blocos, cronograma);
    }

    private static (IReadOnlyList<FormularioProcesso> Formularios, IReadOnlyList<TermoExigidoFormulario> Termos) LerBlocosDeFormulario(
        LeitorEnvelope leitor, JsonArray blocos, CronogramaDoProcesso cronograma)
    {
        List<FormularioComTermosExigidos> formularios = [];
        for (int indice = 0; indice < blocos.Count; indice++)
        {
            FormularioComTermosExigidos? formulario = LerFormulario(leitor, leitor.ItemObjeto(blocos, indice, "formularios"),
                $"formularios[{indice}]", formularios.LastOrDefault()?.Formulario.Codigo, cronograma);
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
        LeitorEnvelope leitor, JsonObject bloco, string path, CodigoDoFormulario? codigoAnterior, CronogramaDoProcesso cronograma)
    {
        leitor.ExigirChaves(bloco, path, "codigo", "faseId", "obrigatorio", "titulo", "consequenciaDaFaltaDeEnvio", "modeloOrigem", "paginas", "termos");
        DeclaracaoFormulario declaracao = LerDeclaracaoDoFormulario(leitor, bloco, path);
        if (leitor.Falhou)
        {
            return null;
        }

        return CriarFormulario(leitor, path, codigoAnterior, cronograma, declaracao);
    }

    private static DeclaracaoFormulario LerDeclaracaoDoFormulario(LeitorEnvelope leitor, JsonObject bloco, string path)
    {
        CodigoDoFormulario? codigo = CodigoDoFormulario.Criar(leitor.TextoNaoVazio(bloco, "codigo", path, CodigoDoFormulario.TamanhoMaximo)).Value;
        Guid? faseId = leitor.IdentificadorOpcional(bloco, "faseId", path);
        bool obrigatorio = leitor.Booleano(bloco, "obrigatorio", path);
        string? titulo = leitor.TextoOpcional(bloco, "titulo", path, LimitesDoEnvelope.NomeDeCadastro);
        ModeloDeOrigem? modeloDeOrigem = LerModeloDeOrigem(leitor, bloco, path);
        List<PaginaDoFormulario> paginas = LerPaginasDoFormulario(leitor, bloco, path);
        JsonArray itensDeTermo = leitor.Array(bloco, "termos", path);
        ConsequenciaDoDescumprimento? consequencia = leitor.CodigoDeContratoOpcional<ConsequenciaDoDescumprimento>(bloco, "consequenciaDaFaltaDeEnvio", path);
        return new DeclaracaoFormulario(codigo, faseId, obrigatorio, titulo, modeloDeOrigem, paginas, itensDeTermo, consequencia);
    }

    private sealed record DeclaracaoFormulario(
        CodigoDoFormulario? Codigo, Guid? FaseId, bool Obrigatorio, string? Titulo, ModeloDeOrigem? ModeloDeOrigem,
        IReadOnlyList<PaginaDoFormulario> Paginas, JsonArray Termos, ConsequenciaDoDescumprimento? Consequencia);

    private static FormularioComTermosExigidos? CriarFormulario(
        LeitorEnvelope leitor, string path, CodigoDoFormulario? codigoAnterior, CronogramaDoProcesso cronograma, DeclaracaoFormulario declaracao)
    {
        if (declaracao.Codigo is not { } codigo || !EmOrdemCrescente(codigo, codigoAnterior))
        {
            return leitor.Recusar<FormularioComTermosExigidos?>($"'{path}.codigo' fora do formato, repetido ou fora da ordem ordinal.");
        }
        Result<FormularioProcesso> formulario = FormularioProcesso.Criar(new FormularioDeclarado(codigo, declaracao.FaseId, declaracao.Obrigatorio),
            cronograma, declaracao.Titulo, declaracao.Paginas, declaracao.ModeloDeOrigem);
        if (formulario.IsFailure)
        {
            return leitor.Propagar<FormularioComTermosExigidos?>(formulario.Errors, path);
        }
        formulario.Value!.DeclararConsequenciaDaFaltaDeEnvio(declaracao.Consequencia);
        List<TermoExigidoFormulario> termos = LerTermos(leitor, declaracao.Termos, path, formulario.Value!);
        return leitor.Falhou ? null : new FormularioComTermosExigidos(formulario.Value!, termos);
    }

    private static bool EmOrdemCrescente(CodigoDoFormulario codigo, CodigoDoFormulario? anterior) =>
        anterior is null || CodigoDoFormulario.OrdemOrdinal.Compare(anterior, codigo) < 0;

    private static List<TermoExigidoFormulario> LerTermos(
        LeitorEnvelope leitor, JsonArray itens, string path, FormularioProcesso formulario)
    {
        List<TermoExigidoFormulario> termos = [];
        for (int indice = 0; indice < itens.Count; indice++)
        {
            TermoExigidoFormulario? termo = LerTermoExigido(leitor, leitor.ItemObjeto(itens, indice, $"{path}.termos"),
                $"{path}.termos[{indice}]", formulario, termos);
            if (termo is null)
            {
                return [];
            }

            termos.Add(termo);
        }

        return termos;
    }

    private static bool SegueOsTermosAnteriores(TermoExigidoFormulario termo, IReadOnlyList<TermoExigidoFormulario> anteriores) =>
        (anteriores.Count == 0 || termo.Ordem > anteriores[^1].Ordem)
        && !anteriores.Any(anterior => string.Equals(anterior.Codigo, termo.Codigo, StringComparison.Ordinal));

    internal static ModeloDeOrigem? LerModeloDeOrigem(LeitorEnvelope leitor, JsonObject bloco, string path)
    {
        if (leitor.ObjetoOpcional(bloco, "modeloOrigem", path) is not { } modelo)
        {
            return null;
        }

        string pathModelo = $"{path}.modeloOrigem";
        leitor.ExigirChaves(modelo, pathModelo, "id", "codigo", "nome");
        Guid id = leitor.Identificador(modelo, "id", pathModelo);
        string codigo = leitor.TextoNaoVazio(modelo, "codigo", pathModelo, LimitesDoEnvelope.CodigoModeloDeFormulario);
        string nome = leitor.TextoNaoVazio(modelo, "nome", pathModelo, LimitesDoEnvelope.NomeModeloDeFormulario);
        return leitor.Falhou ? null : new ModeloDeOrigem(id, codigo, nome);
    }

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
        DeclaracaoPagina declaracaoDaPagina = LerDeclaracaoDaPagina(leitor, item, pathPagina);
        if (leitor.Falhou)
        {
            return null;
        }

        return CriarPagina(leitor, pathPagina, ordemAnterior, declaracaoDaPagina);
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

    private static PaginaDoFormulario? CriarPagina(LeitorEnvelope leitor, string pathPagina, int? ordemAnterior, DeclaracaoPagina declaracaoDaPagina)
    {
        Result<PredicadoDnf?> exibicao = LeituraDasCondicoes.PredicadoOpcional(declaracaoDaPagina.Exibicao);
        // A forma da página não depende da exibição recusada; ela é conferida com uma condição que não acusa nada.
        Result<PaginaDoFormulario> pagina = PaginaDoFormulario.Criar(
            new PaginaEstrutura(declaracaoDaPagina.Codigo, declaracaoDaPagina.Ordem, declaracaoDaPagina.Tipo, declaracaoDaPagina.Bloco), declaracaoDaPagina.Titulo, declaracaoDaPagina.Descricao, declaracaoDaPagina.Aviso,
            exibicao.Value ?? (declaracaoDaPagina.Exibicao.Count > 0 ? PredicadoDnf.SemClausula : null));
        IReadOnlyList<FieldError> causas = [.. exibicao.Errors.Select(causa => causa.NoCaminhoDoItem("exibicao")),
            .. pagina.Errors, .. RecusaDaOrdem(pagina.IsSuccess, pathPagina, ordemAnterior, declaracaoDaPagina.Ordem)];
        return causas.Count > 0 ? leitor.PropagarCausasDoItem<PaginaDoFormulario?>(causas, pathPagina) : pagina.Value;
    }

    // O encoder emite as páginas em ordem estritamente crescente; outra forma só vem de envelope
    // adulterado e não reproduziria os bytes. Página de forma recusada não tem ordem confiável para comparar.
    private static IEnumerable<FieldError> RecusaDaOrdem(bool formaValida, string pathPagina, int? ordemAnterior, int ordem) =>
        formaValida && ordem <= ordemAnterior
            ? [new FieldError(null, new DomainError(ErrosCodecEnvelope.EnvelopeMalformado, $"'{pathPagina}' quebra a ordem crescente das páginas."))]
            : [];

    private static TermoExigidoFormulario? LerTermoExigido(
        LeitorEnvelope leitor, JsonObject item, string path, FormularioProcesso formulario, IReadOnlyList<TermoExigidoFormulario> anteriores)
    {
        if (leitor.Falhou)
        {
            return null;
        }

        leitor.ExigirChaves(item, path, "codigo", "ordem", "termoId", "versaoId", "termoCodigo", "versaoNumero", "nome", "texto", "baseLegal",
            "formaAceite", "hashVersao", "exibicao", "obrigatoriedade");

        DeclaracaoTermo declaracao = LerDeclaracaoDoTermo(leitor, item, path);
        return leitor.Falhou ? null : CriarTermo(leitor, item, path, (formulario, anteriores), declaracao);
    }

    private sealed record DeclaracaoTermo(string Codigo, int Ordem, VersaoTermoEscolhida Versao, IReadOnlyList<LinhaDoPredicado> Exibicao);

    private static DeclaracaoTermo LerDeclaracaoDoTermo(LeitorEnvelope leitor, JsonObject item, string path) => new(
        leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.CodigoTermoExigido),
        leitor.Inteiro(item, "ordem", path),
        LerVersaoDoTermo(leitor, item, path),
        LeituraDasCondicoes.LerDnf(leitor, item, "exibicao", path));

    private static TermoExigidoFormulario? CriarTermo(
        LeitorEnvelope leitor, JsonObject item, string path, (FormularioProcesso Formulario, IReadOnlyList<TermoExigidoFormulario> Anteriores) contexto,
        DeclaracaoTermo declaracao)
    {
        Result<PredicadoDnf?> exibicao = LeituraDasCondicoes.PredicadoOpcional(declaracao.Exibicao);
        if (LeituraDasCondicoes.LerObrigatoriedade(leitor, item, path) is not { } obrigatoriedade)
        {
            // Formato quebrado encerra o item; só a recusa de regra da exibição, lida antes dele, sai junto.
            return exibicao.IsFailure ? leitor.PropagarCausasDoItem<TermoExigidoFormulario?>(CausasDaExibicao(exibicao), path) : null;
        }

        // O termo só confere código e ordem; a exibição e a obrigatoriedade recusadas já têm a recusa própria e não acusam nada aqui.
        Result<TermoExigidoFormulario> termo = TermoExigidoFormulario.Criar(
            declaracao.Codigo, declaracao.Ordem, declaracao.Versao, exibicao.Value, obrigatoriedade.Value ?? Obrigatoriedade.Nunca, contexto.Formulario);
        IReadOnlyList<FieldError> causas = [.. CausasDaExibicao(exibicao), .. obrigatoriedade.Errors, .. termo.Errors,
            .. RecusaDaOrdemEntreTermos(termo, contexto.Anteriores, path)];
        return causas.Count > 0 ? leitor.PropagarCausasDoItem<TermoExigidoFormulario?>(causas, path) : termo.Value;
    }

    private static IReadOnlyList<FieldError> CausasDaExibicao(Result<PredicadoDnf?> exibicao) =>
        [.. exibicao.Errors.Select(causa => causa.NoCaminhoDoItem("exibicao"))];

    private static IEnumerable<FieldError> RecusaDaOrdemEntreTermos(
        Result<TermoExigidoFormulario> termo, IReadOnlyList<TermoExigidoFormulario> anteriores, string path) =>
        termo.IsSuccess && !SegueOsTermosAnteriores(termo.Value!, anteriores)
            ? [new FieldError(null, new DomainError(ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}' repete código ou quebra a ordem crescente dos termos."))]
            : [];

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
