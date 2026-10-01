namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;

/// <summary>
/// A forma de um item do formulário, a mesma no formulário do processo e no modelo: o código do
/// fato, a ordem, o rótulo, o tipo de campo, a ajuda, o formato e as restrições de valor.
/// Acumula toda violação independente em vez de parar na primeira (ADR-0125).
/// </summary>
public static class FormaDoItem
{
    public const int FatoCodigoMaxLength = 60;

    /// <summary>A mesma grandeza de um nome de cadastro curto; o envelope aplica o mesmo limite.</summary>
    public const int RotuloMaxLength = 300;

    public const int FormatoMaxLength = 30;
    public const int AjudaMaxLength = 1000;

    /// <summary>As casas decimais dos limites da faixa numérica, as mesmas com que o edital os congela.</summary>
    public const int CasasDecimaisDaFaixa = 4;

    /// <summary>
    /// Teto de itens de um formulário. O maior formulário do edital de Medicina 2027 tem algumas
    /// dezenas de campos; o teto dá folga larga sem deixar uma entrada pequena gerar resposta
    /// desproporcional, porque acima dele a lista é recusada inteira, sem um erro por item.
    /// </summary>
    public const int MaximoDeItens = 200;

    /// <summary>
    /// A quantidade de itens não depende do catálogo: existe separada para quem recebe a lista
    /// recusá-la acima do teto antes de ler o catálogo e de conferir item a item.
    /// </summary>
    public static List<FieldError> ValidarQuantidade(int quantidade)
    {
        List<FieldError> erros = [];
        if (quantidade > MaximoDeItens)
        {
            erros.Add(new("itens", new DomainError(
                ItemFormularioErrorCodes.ItensEmExcesso, $"O formulário admite no máximo {MaximoDeItens} itens; vieram {quantidade}.")));
        }

        return erros;
    }

    /// <summary>
    /// Os quatro campos que não dependem do catálogo nem das regras: existe separada para quem
    /// recebe a lista inteira conferir a forma de todos os itens antes de resolver o catálogo, e
    /// assim um código vazio não cai num "fato desconhecido", menos específico.
    /// </summary>
    public static List<FieldError> ValidarFormaBasica(string? fatoCodigo, int ordem, string? rotulo, TipoRenderizacao tipoRenderizacao)
    {
        List<FieldError> erros = [];

        if (string.IsNullOrWhiteSpace(fatoCodigo))
        {
            erros.Add(new("fatoCodigo", new DomainError(ItemFormularioErrorCodes.FatoCodigoObrigatorio, "O código do fato do item é obrigatório.")));
        }
        else if (fatoCodigo.Trim().Length > FatoCodigoMaxLength)
        {
            erros.Add(new("fatoCodigo", new DomainError(
                ItemFormularioErrorCodes.FatoCodigoTamanho, $"O código do fato do item deve ter no máximo {FatoCodigoMaxLength} caracteres.")));
        }

        if (ordem < 0)
        {
            erros.Add(new("ordem", new DomainError(ItemFormularioErrorCodes.OrdemInvalida, "A ordem do item não pode ser negativa.")));
        }

        if (string.IsNullOrWhiteSpace(rotulo))
        {
            erros.Add(new("rotulo", new DomainError(ItemFormularioErrorCodes.RotuloObrigatorio, "O rótulo do item é obrigatório.")));
        }
        else if (rotulo.Trim().Length > RotuloMaxLength)
        {
            erros.Add(new("rotulo", new DomainError(
                ItemFormularioErrorCodes.RotuloTamanho, $"O rótulo do item deve ter no máximo {RotuloMaxLength} caracteres.")));
        }

        if (tipoRenderizacao == TipoRenderizacao.Nenhuma || !Enum.IsDefined(tipoRenderizacao))
        {
            erros.Add(new("tipoRenderizacao", new DomainError(
                ItemFormularioErrorCodes.TipoRenderizacaoObrigatorio, "O tipo de campo do item é obrigatório.")));
        }

        return erros;
    }

    /// <summary>
    /// A forma inteira do item: a básica, a ajuda, o formato (só o campo de texto tem), nenhuma
    /// regra citando o próprio fato e as restrições cabendo no campo. A autorreferência é ciclo de
    /// comprimento um, recusada aqui com um erro específico, sem precisar conhecer os outros itens.
    /// </summary>
    public static List<FieldError> Conferir(
        string? fatoCodigo,
        int ordem,
        string? rotulo,
        TipoRenderizacao tipoRenderizacao,
        string? formato,
        string? ajuda,
        IEnumerable<string> fatosCitadosPelaExibicao,
        Obrigatoriedade obrigatoriedade,
        IReadOnlyList<RestricaoValor> restricoes)
    {
        ArgumentNullException.ThrowIfNull(fatosCitadosPelaExibicao);
        ArgumentNullException.ThrowIfNull(obrigatoriedade);
        ArgumentNullException.ThrowIfNull(restricoes);

        List<FieldError> erros = ValidarFormaBasica(fatoCodigo, ordem, rotulo, tipoRenderizacao);

        if (TextoOpcional(ajuda) is { Length: > AjudaMaxLength })
        {
            erros.Add(new("ajuda", new DomainError(
                ItemFormularioErrorCodes.AjudaTamanho, $"A ajuda do campo tem no máximo {AjudaMaxLength} caracteres.")));
        }

        string? formatoNormalizado = TextoOpcional(formato);
        if ((tipoRenderizacao == TipoRenderizacao.Texto) != (formatoNormalizado is not null)
            || formatoNormalizado is { Length: > FormatoMaxLength })
        {
            erros.Add(new("formato", new DomainError(
                ItemFormularioErrorCodes.FormatoIncoerente,
                $"O campo de texto tem o formato do fato no catálogo, com no máximo {FormatoMaxLength} caracteres; os demais campos não têm formato.")));
        }

        string codigo = fatoCodigo?.Trim() ?? string.Empty;
        if (fatosCitadosPelaExibicao.Contains(codigo, StringComparer.Ordinal))
        {
            erros.Add(new("precondicao", new DomainError(ItemFormularioErrorCodes.RegraAutorreferente, "A exibição cita o próprio fato.")));
        }

        if (obrigatoriedade.FatosCitados.Contains(codigo, StringComparer.Ordinal))
        {
            erros.Add(new("predicadoObrigatoriedade", new DomainError(
                ItemFormularioErrorCodes.RegraAutorreferente, "A obrigatoriedade cita o próprio fato.")));
        }

        erros.AddRange(ConferirRestricoes(codigo, tipoRenderizacao, restricoes));
        return erros;
    }

    /// <summary>Se o tipo de restrição se aplica ao tipo de campo.</summary>
    public static bool RestricaoCabeNoCampo(TipoRestricaoValor restricao, TipoRenderizacao campo) =>
        campo != TipoRenderizacao.Nenhuma && restricao switch
        {
            TipoRestricaoValor.FaixaNumerica => campo == TipoRenderizacao.Numero,
            TipoRestricaoValor.TamanhoTexto => campo == TipoRenderizacao.Texto,
            TipoRestricaoValor.OpcoesPermitidas or TipoRestricaoValor.OpcoesDasRespostas => campo.EhSelecao(),
            TipoRestricaoValor.Nenhuma => false,
            _ => throw new ArgumentOutOfRangeException(nameof(restricao), restricao, "Tipo de restrição desconhecido."),
        };

    /// <summary>O texto aparado, ou nulo quando vazio — a forma em que o item guarda ajuda e formato.</summary>
    public static string? TextoOpcional(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    /// <summary>
    /// As restrições cabem no tipo do campo — faixa só no numérico, tamanho só no de texto, opções
    /// só no de seleção —, não se repetem por tipo, não citam o próprio fato, e os limites da faixa
    /// cabem nas casas decimais do edital.
    /// </summary>
    private static IEnumerable<FieldError> ConferirRestricoes(string codigo, TipoRenderizacao tipoRenderizacao, IReadOnlyList<RestricaoValor> restricoes)
    {
        if (RestricoesDeValor.TipoRepetido(restricoes) is { } repetido)
        {
            yield return new("restricoes", repetido);
        }

        for (int indice = 0; indice < restricoes.Count; indice++)
        {
            if (!RestricaoCabeNoCampo(restricoes[indice].Tipo, tipoRenderizacao))
            {
                yield return new($"restricoes[{indice}]", new DomainError(
                    ItemFormularioErrorCodes.RestricaoIncoerente,
                    "A faixa numérica só se aplica ao campo numérico, o tamanho só ao de texto e as opções só ao de seleção."));
            }

            if (restricoes[indice] is FaixaNumerica faixa
                && new[] { faixa.Minimo, faixa.Maximo }.Any(static limite => limite is { } v && Math.Round(v, CasasDecimaisDaFaixa) != v))
            {
                yield return new($"restricoes[{indice}]", new DomainError(
                    RestricaoValorErrorCodes.LimitesIncoerentes,
                    $"Os limites da faixa numérica têm no máximo {CasasDecimaisDaFaixa} casas decimais."));
            }

            if (restricoes[indice].FatosCitados.Contains(codigo, StringComparer.Ordinal))
            {
                yield return new($"restricoes[{indice}]", new DomainError(ItemFormularioErrorCodes.RegraAutorreferente, "A restrição cita o próprio fato."));
            }
        }
    }
}

/// <summary>
/// A forma de um grupo repetível do formulário, a mesma no processo e no modelo (UNI-REQ-0146): o
/// código, a ordem, o rótulo, o mínimo e o máximo de ocorrências e os campos de cada ocorrência.
/// Acumula toda violação independente em vez de parar na primeira (ADR-0125).
/// </summary>
public static class FormaDoGrupo
{
    public const int CodigoMaxLength = 60;

    /// <summary>
    /// Teto de ocorrências de um grupo. A composição familiar, maior grupo do edital de Medicina
    /// 2027, cabe com folga; acima disso a lista de membros deixa de ser formulário de candidato.
    /// </summary>
    public const int MaximoDeOcorrencias = 20;

    /// <summary>Teto de campos de cada ocorrência: um membro da família tem poucos dados declarados.</summary>
    public const int MaximoDeSubitens = 30;

    /// <summary>O mínimo vai de zero até o máximo, e o máximo, de um até o teto de ocorrências.</summary>
    public static bool ContagemValida(int minimo, int maximo) =>
        minimo >= 0 && maximo >= 1 && maximo >= minimo && maximo <= MaximoDeOcorrencias;

    /// <summary>O grupo tem de um até o teto de campos por ocorrência.</summary>
    public static bool QuantidadeDeCamposValida(int quantidade) => quantidade is >= 1 and <= MaximoDeSubitens;

    /// <summary>
    /// Código e rótulo obrigatórios e limitados, ordem não negativa, mínimo de zero até o máximo,
    /// máximo de um até o teto, ao menos um campo e no máximo o teto, e nenhuma regra do grupo
    /// citando o próprio grupo ou os campos dele — fora da ocorrência, eles não têm valor único.
    /// </summary>
    public static List<FieldError> Conferir(
        string? codigo,
        int ordem,
        string? rotulo,
        int minimo,
        int maximo,
        IReadOnlyCollection<string> codigosDosSubitens,
        IEnumerable<string> fatosCitadosPelaExibicao,
        Obrigatoriedade obrigatoriedade)
    {
        ArgumentNullException.ThrowIfNull(codigosDosSubitens);
        ArgumentNullException.ThrowIfNull(fatosCitadosPelaExibicao);
        ArgumentNullException.ThrowIfNull(obrigatoriedade);

        List<FieldError> erros = [];
        void Recusar(string campo, string codigoErro, string mensagem) => erros.Add(new(campo, new DomainError(codigoErro, mensagem)));

        string codigoAparado = codigo?.Trim() ?? string.Empty;
        if (codigoAparado.Length is 0 or > CodigoMaxLength)
        {
            Recusar("codigo", GrupoFormularioErrorCodes.CodigoInvalido, $"O código do grupo é obrigatório e tem no máximo {CodigoMaxLength} caracteres.");
        }

        if (ordem < 0)
        {
            Recusar("ordem", GrupoFormularioErrorCodes.OrdemInvalida, "A ordem do grupo não pode ser negativa.");
        }

        if ((rotulo?.Trim() ?? string.Empty).Length is 0 or > FormaDoItem.RotuloMaxLength)
        {
            Recusar("rotulo", GrupoFormularioErrorCodes.RotuloInvalido,
                $"O rótulo do grupo é obrigatório e tem no máximo {FormaDoItem.RotuloMaxLength} caracteres.");
        }

        string contagem = $"O grupo admite de zero a {MaximoDeOcorrencias} ocorrências: o mínimo não passa do máximo, e o máximo é ao menos um.";
        if (minimo < 0)
        {
            Recusar("minimo", GrupoFormularioErrorCodes.ContagemIncoerente, contagem);
        }
        else if (!ContagemValida(minimo, maximo))
        {
            Recusar("maximo", GrupoFormularioErrorCodes.ContagemIncoerente, contagem);
        }

        if (!QuantidadeDeCamposValida(codigosDosSubitens.Count))
        {
            Recusar("subitens", GrupoFormularioErrorCodes.SubitensForaDoLimite,
                $"O grupo tem de um a {MaximoDeSubitens} campos por ocorrência.");
        }

        HashSet<string> doGrupo = new(codigosDosSubitens.Append(codigoAparado), StringComparer.Ordinal);
        foreach ((string campo, IEnumerable<string> citados) in new[] { ("exibicao", fatosCitadosPelaExibicao), ("predicadoObrigatoriedade", obrigatoriedade.FatosCitados) })
        {
            if (citados.FirstOrDefault(doGrupo.Contains) is { } citado)
            {
                Recusar(campo, GrupoFormularioErrorCodes.RegraAutorreferente,
                    $"Uma regra do grupo cita '{citado}', do próprio grupo — a exibição e a obrigatoriedade do grupo se decidem antes das ocorrências.");
            }
        }

        return erros;
    }
}

/// <summary>O cabeçalho do formulário, o mesmo no processo e no modelo: a finalidade e o título.</summary>
public static class FormaDoCabecalho
{
    public const int TituloMaxLength = 300;

    /// <summary>A finalidade é uma das três do formulário, e o título, quando há, cabe no limite.</summary>
    public static List<FieldError> Validar(FinalidadeFormulario finalidade, string? titulo) =>
        [.. ValidarFinalidade(finalidade), .. ValidarTitulo(titulo)];

    /// <summary>A finalidade é inscrição, isenção de taxa ou habilitação.</summary>
    public static List<FieldError> ValidarFinalidade(FinalidadeFormulario finalidade) =>
        finalidade == FinalidadeFormulario.Nenhuma || !Enum.IsDefined(finalidade)
            ? [new("finalidade", new DomainError(EstruturaFormularioErrorCodes.FinalidadeInvalida,
                "A finalidade do formulário é inscrição, isenção de taxa ou habilitação."))]
            : [];

    /// <summary>O título, quando há, cabe no limite.</summary>
    public static List<FieldError> ValidarTitulo(string? titulo) =>
        FormaDoItem.TextoOpcional(titulo) is { Length: > TituloMaxLength }
            ? [new("titulo", new DomainError(EstruturaFormularioErrorCodes.TituloTamanho,
                $"O título do formulário deve ter no máximo {TituloMaxLength} caracteres."))]
            : [];
}

/// <summary>A forma de uma etapa do formulário, a mesma no processo e no modelo.</summary>
public static class FormaDaEtapa
{
    public const int CodigoMaxLength = 60;
    public const int TituloMaxLength = 300;
    public const int TextoMaxLength = 2000;

    /// <summary>
    /// Código e título obrigatórios e limitados, ordem não negativa, descrição e aviso limitados, e
    /// exibição condicional só na seção — o bloco de sistema aparece sempre.
    /// </summary>
    public static List<FieldError> Conferir(
        string? codigo, int ordem, TipoEtapaFormulario tipo, string? titulo, string? descricao, string? aviso, bool temExibicao)
    {
        List<FieldError> erros = [];
        void Recusar(string campo, string codigoErro, string mensagem) => erros.Add(new(campo, new DomainError(codigoErro, mensagem)));

        if ((codigo?.Trim() ?? string.Empty).Length is 0 or > CodigoMaxLength)
        {
            Recusar("codigo", EstruturaFormularioErrorCodes.EtapaCodigoInvalido,
                $"O código da etapa é obrigatório e tem no máximo {CodigoMaxLength} caracteres.");
        }

        if (ordem < 0)
        {
            Recusar("ordem", EstruturaFormularioErrorCodes.EtapaOrdemInvalida, "A ordem da etapa não pode ser negativa.");
        }

        if ((titulo?.Trim() ?? string.Empty).Length is 0 or > TituloMaxLength)
        {
            Recusar("titulo", EstruturaFormularioErrorCodes.EtapaTituloInvalido,
                $"O título da etapa é obrigatório e tem no máximo {TituloMaxLength} caracteres.");
        }

        if (FormaDoItem.TextoOpcional(descricao) is { Length: > TextoMaxLength })
        {
            Recusar("descricao", EstruturaFormularioErrorCodes.EtapaTextoTamanho, $"A descrição da etapa tem no máximo {TextoMaxLength} caracteres.");
        }

        if (FormaDoItem.TextoOpcional(aviso) is { Length: > TextoMaxLength })
        {
            Recusar("aviso", EstruturaFormularioErrorCodes.EtapaTextoTamanho, $"O aviso da etapa tem no máximo {TextoMaxLength} caracteres.");
        }

        if (temExibicao && tipo != TipoEtapaFormulario.Secao)
        {
            Recusar("exibicao", EstruturaFormularioErrorCodes.ExibicaoForaDeSecao,
                "Só a seção tem exibição condicional; o bloco de sistema aparece sempre.");
        }

        return erros;
    }
}

/// <summary>A forma de um termo exigido pelo formulário, a mesma no processo e no modelo.</summary>
public static class FormaDoTermo
{
    public const int CodigoMaxLength = 60;

    /// <summary>A forma do termo que não depende do catálogo: confere todo o payload antes de ler as versões.</summary>
    public static List<FieldError> ValidarFormaBasica(string? codigo, int ordem)
    {
        List<FieldError> erros = [];
        if (string.IsNullOrWhiteSpace(codigo))
        {
            erros.Add(new("codigo", new DomainError(TermoFormularioErrorCodes.CodigoObrigatorio, "O código do termo exigido é obrigatório.")));
        }
        else if (codigo.Trim().Length > CodigoMaxLength)
        {
            erros.Add(new("codigo", new DomainError(
                TermoFormularioErrorCodes.CodigoTamanho, $"O código do termo exigido deve ter no máximo {CodigoMaxLength} caracteres.")));
        }

        if (ordem < 0)
        {
            erros.Add(new("ordem", new DomainError(TermoFormularioErrorCodes.OrdemInvalida, "A ordem do termo não pode ser negativa.")));
        }

        return erros;
    }

    /// <summary>O código em NFC; o que não se normaliza é comparado como veio, e a recusa dele é de quem guarda o texto.</summary>
    private static string EmNfc(string codigo) => TextoNormalizavel.TentarNormalizar(codigo, out string normalizado) ? normalizado : codigo;

    /// <summary>
    /// Código e ordem únicos entre os termos do formulário, com o código na forma que o termo guarda
    /// (aparado, em NFC). Termo ausente ou sem código não entra na conferência do código — a recusa
    /// dele é a de forma.
    /// </summary>
    public static List<FieldError> ConferirUnicidade(IReadOnlyList<(string? Codigo, int Ordem)?> termos)
    {
        ArgumentNullException.ThrowIfNull(termos);

        List<FieldError> erros = [];
        HashSet<string> codigos = new(StringComparer.Ordinal);
        HashSet<int> ordens = [];
        for (int i = 0; i < termos.Count; i++)
        {
            if (termos[i] is not { } termo)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(termo.Codigo) && !codigos.Add(EmNfc(termo.Codigo.Trim())))
            {
                erros.Add(new($"termos[{i}].codigo", new DomainError(
                    TermoFormularioErrorCodes.CodigoDuplicado, $"O código '{termo.Codigo.Trim()}' aparece em mais de um termo do formulário.")));
            }

            if (!ordens.Add(termo.Ordem))
            {
                erros.Add(new($"termos[{i}].ordem", new DomainError(
                    TermoFormularioErrorCodes.OrdemDuplicada, "Dois termos do formulário têm a mesma ordem.")));
            }
        }

        return erros;
    }
}
