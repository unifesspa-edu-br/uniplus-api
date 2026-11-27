namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

using CampoDoMembro = (string FatoCodigo, bool TemExibicao, Unifesspa.UniPlus.Regras.Formularios.Obrigatoriedade Obrigatoriedade, System.Collections.Generic.IReadOnlyList<Unifesspa.UniPlus.Regras.Formularios.RestricaoValor> Restricoes);

/// <summary>
/// Uma leitura da entrada do conteúdo do modelo, pela forma das regras do formulário
/// (<see cref="EntradaDeRegras"/>): um código desconhecido é recusado pelas regras do formulário;
/// predicado, obrigatoriedade e restrição malformados são recusados aqui. O formato do campo de texto
/// é o do fato no catálogo. Acumula as recusas, na ordem em que as partes são lidas, e guarda a posição
/// de cada parte lida na lista da entrada. A parte nula ou com recusa não é lida; a recusa dela fica em
/// <see cref="Recusas"/>, junto com as dos textos que o banco não grava e as da forma dela que não dependem do que foi recusado.
/// </summary>
internal sealed class LeituraDoConteudo
{
    private readonly RecusasDaLeituraDoModelo _recusas = new();
    private readonly LeituraDosCamposDoModelo _campos;
    private readonly List<CampoDeGrupoNaoLido> _camposDeGruposNaoLidos = [];

    public LeituraDoConteudo(IReadOnlyDictionary<string, string> formatos) => _campos = new(_recusas, formatos);

    /// <summary>As recusas da leitura, na ordem em que as partes foram lidas.</summary>
    public IReadOnlyList<FieldError> Recusas => _recusas.Recusas;

    /// <summary>
    /// Lê as partes na ordem das recusas: páginas, itens, termos, grupos, documentos e grupos de documentos. O teto de
    /// itens, que conta o pedido inteiro, é conferido aqui, antes dos itens; a repetição de código ou de ordem dos
    /// termos é conferida aqui, sobre o pedido inteiro, quando algo foi recusado. Com tudo lido, as duas são do domínio.
    /// </summary>
    public ConteudoLido Ler(ConteudoDoModeloInput? entrada)
    {
        ConteudoLido partes = LerPartes(entrada);
        bool porInteiro = _recusas.Recusas.Count == 0;
        if (!porInteiro)
        {
            _recusas.Recusas.AddRange(FormaDoTermo.ConferirUnicidade(
                [.. (entrada?.Termos ?? []).Select(static t => t is null ? null : ((string?, int)?)(t.Codigo, t.Ordem))]));
        }

        return partes with { PorInteiro = porInteiro };
    }

    private ConteudoLido LerPartes(ConteudoDoModeloInput? entrada)
    {
        LeituraDosDocumentosDoModelo documentos = new(_recusas);
        return new(
            entrada?.Titulo,
            _recusas.Lista(entrada?.Paginas, "paginas", LerPagina),
            LerItens(entrada),
            _recusas.Lista(entrada?.Termos, "termos", LerTermo),
            [.. (entrada?.Pressupostos ?? []).Select(static p => p ?? string.Empty)],
            new GruposLidos(_recusas.ListaComPosicao(entrada?.Grupos, "grupos", LerGrupo), _camposDeGruposNaoLidos),
            documentos.Ler(entrada?.ConjuntoDeDocumentos),
            new PartesNaoLidas(_campos.CamposNaoLidos, documentos.FatosPorOpcaoNaoLidos),
            false);
    }

    private List<(int Indice, ItemDoModelo Item)> LerItens(ConteudoDoModeloInput? entrada)
    {
        _recusas.Recusas.AddRange(FormaDoItem.ValidarQuantidade(FormaDoItem.QuantidadeNoTeto(
            entrada?.Itens?.Count ?? 0, (entrada?.Grupos ?? []).Select(static g => g?.Subitens?.Count ?? 0))));
        return _recusas.ListaComPosicao(entrada?.Itens, "itens", (item, posicao, campo) => _campos.LerItem(item, campo, new LugarDoCampo(posicao)));
    }

    private PaginaDoModelo? LerPagina(PaginaDoFormularioInput entrada, string campo)
    {
        int recusasAntes = _recusas.Recusas.Count;
        PredicadoDnf? exibicao = _recusas.LerPredicado(entrada.Exibicao, $"{campo}.exibicao");
        TipoPaginaFormulario? tipo = LerTipoDaPagina(entrada.Tipo, $"{campo}.tipo");
        BlocoSistema? bloco = LerBlocoDaPagina(entrada.Bloco, $"{campo}.bloco");
        if (_recusas.RecusouDesde(recusasAntes))
        {
            RecusarPaginaNaoLida(entrada, campo, tipo, exibicao);
            return null;
        }

        return new PaginaDoModelo(
            entrada.Codigo, entrada.Ordem, tipo!.Value, bloco,
            entrada.Apresentacao?.Titulo ?? string.Empty, entrada.Apresentacao?.Descricao, entrada.Apresentacao?.Aviso, exibicao);
    }

    /// <summary>
    /// Recusa os textos e a forma da página que a leitura recusou. A exibição conta como enviada mesmo malformada: que só a
    /// seção a tem não depende de ela estar bem formada.
    /// </summary>
    private void RecusarPaginaNaoLida(PaginaDoFormularioInput entrada, string campo, TipoPaginaFormulario? tipo, PredicadoDnf? exibicao)
    {
        _recusas.Recusas.AddRange(RecusasDaLeituraDoModelo.TextosNaoGravaveis(TextosDoConteudoDoModelo.DaPagina(
            campo, entrada.Apresentacao?.Titulo, entrada.Apresentacao?.Descricao, entrada.Apresentacao?.Aviso, exibicao)));
        _recusas.Recusas.AddRange(FormaDaPagina.Conferir(
            entrada.Codigo, entrada.Ordem, tipo, entrada.Apresentacao?.Titulo, entrada.Apresentacao?.Descricao, entrada.Apresentacao?.Aviso, entrada.Exibicao is { Count: > 0 })
            .Select(erro => erro.NoCaminhoDoItem(campo)));
    }

    private TipoPaginaFormulario? LerTipoDaPagina(string? codigo, string campo)
    {
        Result<TipoPaginaFormulario> tipo = TipoEBlocoDaEntrada.LerTipo(codigo);
        if (tipo.IsFailure)
        {
            _recusas.Recusas.Add(new(campo, tipo.Error!));
        }

        return tipo.IsSuccess ? tipo.Value : null;
    }

    private BlocoSistema? LerBlocoDaPagina(string? codigo, string campo)
    {
        Result<BlocoSistema?> bloco = TipoEBlocoDaEntrada.LerBloco(codigo);
        if (bloco.IsFailure)
        {
            _recusas.Recusas.Add(new(campo, bloco.Error!));
        }

        return bloco.IsSuccess ? bloco.Value : null;
    }

    private TermoDoModelo? LerTermo(TermoExigidoInput entrada, string campo)
    {
        int recusasAntes = _recusas.Recusas.Count;
        (PredicadoDnf? exibicao, Obrigatoriedade? obrigatoriedade) = LerExibicaoEObrigatoriedadePorPartes(entrada.Regras, $"{campo}.{TermoExigidoInput.CaminhoDasRegras}");
        if (_recusas.RecusouDesde(recusasAntes))
        {
            RecusarTermoNaoLido(entrada, campo, exibicao, obrigatoriedade);
            return null;
        }

        return new TermoDoModelo(
            entrada.Codigo, entrada.Ordem, entrada.Termo?.TermoId ?? Guid.Empty, entrada.Termo?.VersaoId ?? Guid.Empty,
            new ExibicaoEObrigatoriedade(exibicao, obrigatoriedade!));
    }

    private void RecusarTermoNaoLido(TermoExigidoInput entrada, string campo, PredicadoDnf? exibicao, Obrigatoriedade? obrigatoriedade)
    {
        _recusas.Recusas.AddRange(RecusasDaLeituraDoModelo.TextosNaoGravaveis(
            TextosDoConteudoDoModelo.DoTermo(campo, exibicao, obrigatoriedade?.Predicado)));
        _recusas.Recusas.AddRange(FormaDoTermo.ValidarFormaBasica(entrada.Codigo, entrada.Ordem).Select(erro => erro.NoCaminhoDoItem(campo)));
    }

    private GrupoDoModelo? LerGrupo(GrupoColetadoInput entrada, int posicao, string campo)
    {
        int recusasAntes = _recusas.Recusas.Count;
        OcorrenciasLidasDoGrupo ocorrencias = OcorrenciasLidasDoGrupo.Ler(entrada.Ocorrencias);
        _recusas.Recusas.AddRange(ocorrencias.Recusas.Select(recusa => recusa.NoCaminhoDoItem(campo)));
        (PredicadoDnf? exibicao, Obrigatoriedade? obrigatoriedade) = LerExibicaoEObrigatoriedadePorPartes(entrada.Regras, $"{campo}.{GrupoColetadoInput.CaminhoDasRegras}");
        int antesDosSubitens = _recusas.Recusas.Count;
        SubitensLidos subitens = _campos.LerSubitens(entrada, campo, posicao);
        return _recusas.RecusouDesde(recusasAntes)
            ? RecusarGrupoNaoLido(entrada, posicao, ocorrencias, (exibicao, obrigatoriedade), subitens, antesDosSubitens)
            : new GrupoDoModelo(
                entrada.Codigo, entrada.Ordem, entrada.PaginaCodigo, entrada.Rotulo, ocorrencias.Declaradas!,
                new ExibicaoEObrigatoriedade(exibicao, obrigatoriedade!), [.. subitens.Lidos.Select(static s => s.Parte)]);
    }

    /// <summary>
    /// O que o grupo que a leitura recusou ainda deve, para que as demais recusas dele saiam junto: os textos e a forma do
    /// próprio grupo, a do candidato como membro, e por último as dos campos, cada uma na posição do campo. Os campos que
    /// a leitura aceitou seguem para a conferência no catálogo. Devolve nulo, o grupo não lido.
    /// </summary>
    private GrupoDoModelo? RecusarGrupoNaoLido(
        GrupoColetadoInput entrada,
        int posicao,
        OcorrenciasLidasDoGrupo ocorrencias,
        (PredicadoDnf? Exibicao, Obrigatoriedade? Obrigatoriedade) regras,
        SubitensLidos subitens,
        int posicaoDosSubitens)
    {
        string campo = $"grupos[{posicao}]";
        _recusas.Recusas.RemoveRange(posicaoDosSubitens, _recusas.Recusas.Count - posicaoDosSubitens);
        _recusas.Recusas.AddRange(RecusasDaLeituraDoModelo.TextosNaoGravaveis(TextosDoConteudoDoModelo.DoGrupo(
            campo, entrada.PaginaCodigo, entrada.Rotulo, regras.Exibicao, regras.Obrigatoriedade?.Predicado)));
        _recusas.Recusas.AddRange(FormaDoGrupoNaEntrada.Conferir(
            entrada, ocorrencias, regras.Exibicao, regras.Obrigatoriedade, CamposDoMembro(entrada, subitens)).Select(erro => erro.NoCaminhoDoItem(campo)));
        _recusas.Recusas.AddRange(subitens.RecusasNaOrdem);
        _camposDeGruposNaoLidos.AddRange(subitens.Lidos.Select(s => new CampoDeGrupoNaoLido(posicao, s.Indice, s.Parte)));
        return null;
    }

    private static CampoDoMembro?[] CamposDoMembro(GrupoColetadoInput entrada, SubitensLidos subitens)
    {
        CampoDoMembro?[] campos = new CampoDoMembro?[entrada.Subitens?.Count ?? 0];
        foreach ((int indice, ItemDoModelo subitem) in subitens.Lidos)
        {
            campos[indice] = (subitem.FatoCodigo, subitem.Regras.Exibicao is not null, subitem.Regras.Obrigatoriedade, subitem.Regras.Restricoes);
        }

        return campos;
    }

    /// <summary>A exibição e a obrigatoriedade lidas cada uma por si: a recusa de uma não impede a leitura da outra.</summary>
    private (PredicadoDnf? Exibicao, Obrigatoriedade? Obrigatoriedade) LerExibicaoEObrigatoriedadePorPartes(ExibicaoEObrigatoriedadeInput? entrada, string campo)
    {
        PredicadoDnf? exibicao = _recusas.LerPredicado(entrada?.Exibicao, $"{campo}.exibicao");
        Obrigatoriedade? obrigatoriedade = _recusas.LerObrigatoriedade(
            entrada?.Obrigatoriedade?.Tipo, entrada?.Obrigatoriedade?.Predicado, $"{campo}.obrigatoriedade", $"{campo}.obrigatoriedade.predicado");
        return (exibicao, obrigatoriedade);
    }
}
