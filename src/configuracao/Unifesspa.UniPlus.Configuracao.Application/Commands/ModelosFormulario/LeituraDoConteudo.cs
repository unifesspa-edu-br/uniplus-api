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

/// <summary>
/// Uma leitura da entrada do conteúdo do modelo, pela forma das regras do formulário
/// (<see cref="EntradaDeRegras"/>): um código desconhecido é recusado pelas regras do formulário;
/// predicado, obrigatoriedade e restrição malformados são recusados aqui. O formato do campo de texto
/// é o do fato no catálogo. Acumula as recusas, na ordem em que as partes são lidas, e guarda a posição
/// de cada parte lida na lista da entrada. A parte nula ou com recusa não é lida; a recusa dela fica em
/// <see cref="Recusas"/>.
/// </summary>
/// <param name="formatos">O formato do campo de texto de cada fato, pelo código na forma gravada.</param>
internal sealed class LeituraDoConteudo(IReadOnlyDictionary<string, string> formatos)
{
    private readonly RecusasDaLeituraDoModelo _recusas = new();

    /// <summary>As recusas da leitura, na ordem em que as partes foram lidas.</summary>
    public IReadOnlyList<FieldError> Recusas => _recusas.Recusas;

    /// <summary>
    /// Lê as partes na ordem dos argumentos, que é a ordem das recusas: páginas, itens, termos, grupos,
    /// documentos e grupos de documentos.
    /// </summary>
    public ConteudoLido Ler(ConteudoDoModeloInput? entrada) => new(
        entrada?.Titulo,
        _recusas.Lista(entrada?.Paginas, "paginas", LerPagina),
        _recusas.Lista(entrada?.Itens, "itens", LerItem),
        _recusas.Lista(entrada?.Termos, "termos", LerTermo),
        [.. (entrada?.Pressupostos ?? []).Select(static p => p ?? string.Empty)],
        _recusas.Lista(entrada?.Grupos, "grupos", LerGrupo),
        new LeituraDosDocumentosDoModelo(_recusas).Ler(entrada?.ConjuntoDeDocumentos));

    private PaginaDoModelo? LerPagina(PaginaDoFormularioInput entrada, string campo)
    {
        int recusasAntes = _recusas.Recusas.Count;
        PredicadoDnf? exibicao = _recusas.LerPredicado(entrada.Exibicao, $"{campo}.exibicao");
        TipoPaginaFormulario? tipo = LerTipoDaPagina(entrada.Tipo, $"{campo}.tipo");
        BlocoSistema? bloco = LerBlocoDaPagina(entrada.Bloco, $"{campo}.bloco");
        return _recusas.RecusouDesde(recusasAntes)
            ? null
            : new PaginaDoModelo(
                entrada.Codigo, entrada.Ordem, tipo!.Value, bloco,
                entrada.Apresentacao?.Titulo ?? string.Empty, entrada.Apresentacao?.Descricao, entrada.Apresentacao?.Aviso, exibicao);
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

    private ItemDoModelo? LerItem(FatoColetadoInput entrada, string campo)
    {
        int recusasAntes = _recusas.Recusas.Count;
        TipoDeCampo? tipo = LerTipoDeCampo(entrada.Apresentacao?.TipoDeCampo, $"{campo}.{FatoColetadoInput.CaminhoDaApresentacao}");
        RegrasDoCampo? regras = LerRegrasDoCampo(entrada.Regras, $"{campo}.{FatoColetadoInput.CaminhoDasRegras}");
        return _recusas.RecusouDesde(recusasAntes)
            ? null
            : new ItemDoModelo(
                entrada.FatoCodigo,
                entrada.Ordem,
                entrada.PaginaCodigo,
                EntradaDeRegras.Apresentacao(entrada.Apresentacao, tipo!.Value),
                FormatoDoCatalogo(entrada.FatoCodigo),
                regras!);
    }

    private RegrasDoCampo? LerRegrasDoCampo(RegrasDoCampoInput? entrada, string campo)
    {
        PredicadoDnf? exibicao = _recusas.LerPredicado(entrada?.Exibicao, $"{campo}.exibicao");
        Obrigatoriedade? obrigatoriedade = LerObrigatoriedade(
            entrada?.Obrigatoriedade?.Tipo, entrada?.Obrigatoriedade?.Predicado, $"{campo}.obrigatoriedade", $"{campo}.obrigatoriedade.predicado");
        List<RestricaoValor> restricoes = LerRestricoes(entrada?.Restricoes, campo);
        Impedimento? impedimento = LerImpedimento(entrada?.Impedimento, campo);
        return obrigatoriedade is null ? null : new RegrasDoCampo(exibicao, obrigatoriedade, restricoes, impedimento);
    }

    private TipoDeCampo? LerTipoDeCampo(string? codigo, string campo)
    {
        Result<TipoDeCampo> tipo = TipoDeCampoDaEntrada.Ler(codigo);
        if (tipo.IsFailure)
        {
            _recusas.Recusas.Add(new($"{campo}.tipoDeCampo", tipo.Error!));
        }

        return tipo.IsSuccess ? tipo.Value : null;
    }

    private TermoDoModelo? LerTermo(TermoExigidoInput entrada, string campo)
    {
        int recusasAntes = _recusas.Recusas.Count;
        ExibicaoEObrigatoriedade? regras = LerExibicaoEObrigatoriedade(entrada.Regras, $"{campo}.{TermoExigidoInput.CaminhoDasRegras}");
        return _recusas.RecusouDesde(recusasAntes)
            ? null
            : new TermoDoModelo(entrada.Codigo, entrada.Ordem, entrada.Termo?.TermoId ?? Guid.Empty, entrada.Termo?.VersaoId ?? Guid.Empty, regras!);
    }

    private GrupoDoModelo? LerGrupo(GrupoColetadoInput entrada, string campo)
    {
        int recusasAntes = _recusas.Recusas.Count;
        OcorrenciasLidasDoGrupo ocorrencias = OcorrenciasLidasDoGrupo.Ler(entrada.Ocorrencias);
        _recusas.Recusas.AddRange(ocorrencias.Recusas.Select(recusa => recusa.NoCaminhoDoItem(campo)));
        ExibicaoEObrigatoriedade? regras = LerExibicaoEObrigatoriedade(entrada.Regras, $"{campo}.{GrupoColetadoInput.CaminhoDasRegras}");
        List<ItemDoModelo> subitens = [.. _recusas.Lista(entrada.Subitens, $"{campo}.subitens", LerItem).Select(static subitem => subitem.Parte)];
        return _recusas.RecusouDesde(recusasAntes)
            ? null
            : new GrupoDoModelo(
                entrada.Codigo, entrada.Ordem, entrada.PaginaCodigo, entrada.Rotulo, ocorrencias.Declaradas!, regras!, subitens);
    }

    private ExibicaoEObrigatoriedade? LerExibicaoEObrigatoriedade(ExibicaoEObrigatoriedadeInput? entrada, string campo)
    {
        PredicadoDnf? exibicao = _recusas.LerPredicado(entrada?.Exibicao, $"{campo}.exibicao");
        Obrigatoriedade? obrigatoriedade = LerObrigatoriedade(
            entrada?.Obrigatoriedade?.Tipo, entrada?.Obrigatoriedade?.Predicado, $"{campo}.obrigatoriedade", $"{campo}.obrigatoriedade.predicado");
        return obrigatoriedade is null ? null : new ExibicaoEObrigatoriedade(exibicao, obrigatoriedade);
    }

    private List<RestricaoValor> LerRestricoes(IReadOnlyList<RestricaoValorInput?>? entradas, string campo)
    {
        List<RestricaoValor> restricoes = [];
        foreach ((int indice, RestricaoValorInput? entrada) in (entradas ?? []).Index())
        {
            if (LerRestricao(entrada, $"{campo}.restricoes[{indice}]") is { } restricao)
            {
                restricoes.Add(restricao);
            }
        }

        return restricoes;
    }

    private RestricaoValor? LerRestricao(RestricaoValorInput? entrada, string campo)
    {
        Result<RestricaoValor> restricao = entrada is null
            ? Result<RestricaoValor>.Failure(new DomainError(RestricaoValorErrorCodes.TipoDesconhecido, "A restrição de valor é nula."))
            : EntradaDeRegras.Restricao(entrada);
        if (restricao.IsFailure)
        {
            _recusas.Recusas.Add(new(campo, restricao.Error!));
        }

        return restricao.Value;
    }

    private Impedimento? LerImpedimento(ImpedimentoInput? entrada, string campo)
    {
        Result<Impedimento?> impedimento = EntradaDeRegras.Impedimento(entrada);
        if (impedimento.IsFailure)
        {
            _recusas.Recusas.AddRange(impedimento.Errors.Select(e => e.NoCaminhoDoItem($"{campo}.impedimento.quando")));
        }

        return impedimento.Value;
    }

    private Obrigatoriedade? LerObrigatoriedade(
        string? codigo, IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? predicadoDeEntrada, string caminhoDoCodigo, string caminhoDoPredicado)
    {
        int recusasAntes = _recusas.Recusas.Count;
        PredicadoDnf? predicado = _recusas.LerPredicado(predicadoDeEntrada, caminhoDoPredicado);
        return _recusas.RecusouDesde(recusasAntes) ? null : ObrigatoriedadeDoCodigo(codigo, predicado, caminhoDoCodigo);
    }

    private Obrigatoriedade? ObrigatoriedadeDoCodigo(string? codigo, PredicadoDnf? predicado, string caminho)
    {
        Obrigatoriedade? obrigatoriedade = EntradaDeRegras.Obrigatoriedade(codigo, predicado);
        if (obrigatoriedade is null)
        {
            _recusas.Recusas.Add(new(caminho, new DomainError(
                ModeloFormularioErrorCodes.ObrigatoriedadeInvalida,
                "A obrigatoriedade é SEMPRE ou NUNCA, sem predicado, ou QUANDO, com predicado.")));
        }

        return obrigatoriedade;
    }

    private string? FormatoDoCatalogo(string? fatoCodigo) =>
        fatoCodigo is { } codigo && formatos.TryGetValue(codigo, out string? formato) ? formato : null;

}
