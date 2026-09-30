namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Para que o formulário coleta respostas: cada processo tem no máximo um por finalidade (UNI-REQ-0144).</summary>
public enum FinalidadeFormulario
{
    Nenhuma = 0,
    Inscricao = 1,
    IsencaoTaxa = 2,
    Habilitacao = 3,
}

/// <summary>Uma etapa do formulário é uma seção de itens ou um bloco que o sistema monta.</summary>
public enum TipoEtapaFormulario
{
    Nenhum = 0,
    Secao = 1,
    Bloco = 2,
}

/// <summary>Os blocos que o sistema monta a partir da configuração do processo.</summary>
public enum BlocoSistema
{
    Nenhum = 0,
    ComprovacaoDocumental = 1,
    ModalidadesCalculadas = 2,
    RevisaoEAceite = 3,
}

/// <summary>Uma etapa, na forma que a estrutura do formulário confere.</summary>
public sealed record EtapaEstrutura(string Codigo, int Ordem, TipoEtapaFormulario Tipo, BlocoSistema Bloco);

/// <summary>Um item, na forma que a estrutura do formulário confere: o fato, a ordem e a seção.</summary>
public sealed record ItemEstrutura(string FatoCodigo, int Ordem, string? EtapaCodigo);

/// <summary>
/// As regras de forma de um formulário, as mesmas no processo e no modelo (ADR-0137): quais blocos
/// a finalidade admite e exige, a revisão e aceite por último, e os itens em seções, na ordem delas.
/// Tokens canônicos UPPER_SNAKE para o wire e o envelope.
/// </summary>
public static class EstruturaFormulario
{
    public const string FinalidadeInscricao = "INSCRICAO";
    public const string FinalidadeIsencaoTaxa = "ISENCAO_TAXA";
    public const string FinalidadeHabilitacao = "HABILITACAO";

    public const string TipoSecao = "SECAO";
    public const string TipoBloco = "BLOCO";

    public const string BlocoComprovacaoDocumental = "COMPROVACAO_DOCUMENTAL";
    public const string BlocoModalidadesCalculadas = "MODALIDADES_CALCULADAS";
    public const string BlocoRevisaoEAceite = "REVISAO_E_ACEITE";

    /// <summary>Os blocos que a finalidade admite: as modalidades calculadas só fazem sentido na inscrição.</summary>
    public static IReadOnlySet<BlocoSistema> BlocosAdmitidos(FinalidadeFormulario finalidade) => finalidade switch
    {
        FinalidadeFormulario.Inscricao =>
            new HashSet<BlocoSistema> { BlocoSistema.ComprovacaoDocumental, BlocoSistema.ModalidadesCalculadas, BlocoSistema.RevisaoEAceite },
        FinalidadeFormulario.IsencaoTaxa or FinalidadeFormulario.Habilitacao =>
            new HashSet<BlocoSistema> { BlocoSistema.ComprovacaoDocumental, BlocoSistema.RevisaoEAceite },
        _ => new HashSet<BlocoSistema>(),
    };

    /// <summary>Os blocos que a finalidade exige: todo formulário termina na revisão e aceite.</summary>
    public static IReadOnlySet<BlocoSistema> BlocosExigidos(FinalidadeFormulario finalidade) =>
        finalidade == FinalidadeFormulario.Nenhuma ? new HashSet<BlocoSistema>() : new HashSet<BlocoSistema> { BlocoSistema.RevisaoEAceite };

    /// <summary>
    /// Confere as etapas: código e ordem únicos, seção sem bloco, bloco admitido pela finalidade e
    /// não repetido, os blocos exigidos presentes e a revisão e aceite como última etapa.
    /// </summary>
    public static IReadOnlyList<FieldError> ValidarEtapas(FinalidadeFormulario finalidade, IReadOnlyList<EtapaEstrutura> etapas)
    {
        ArgumentNullException.ThrowIfNull(etapas);

        List<FieldError> erros = [];
        void Recusar(string campo, string codigo, string mensagem) => erros.Add(new(campo, new DomainError(codigo, mensagem)));

        HashSet<string> codigos = new(StringComparer.Ordinal);
        HashSet<int> ordens = [];
        HashSet<BlocoSistema> blocos = [];
        IReadOnlySet<BlocoSistema> admitidos = BlocosAdmitidos(finalidade);
        for (int i = 0; i < etapas.Count; i++)
        {
            EtapaEstrutura etapa = etapas[i];
            string campo = $"etapas[{i}]";
            if (!codigos.Add(etapa.Codigo))
            {
                Recusar($"{campo}.codigo", EstruturaFormularioErrorCodes.EtapaCodigoDuplicado, $"O código de etapa '{etapa.Codigo}' se repete no formulário.");
            }

            if (!ordens.Add(etapa.Ordem))
            {
                Recusar($"{campo}.ordem", EstruturaFormularioErrorCodes.EtapaOrdemDuplicada, "Duas etapas do formulário têm a mesma ordem.");
            }

            switch (etapa.Tipo)
            {
                case TipoEtapaFormulario.Secao when etapa.Bloco != BlocoSistema.Nenhum:
                    Recusar($"{campo}.bloco", EstruturaFormularioErrorCodes.SecaoComBloco, "Uma seção não declara bloco de sistema.");
                    break;
                case TipoEtapaFormulario.Bloco when !admitidos.Contains(etapa.Bloco):
                    Recusar($"{campo}.bloco", EstruturaFormularioErrorCodes.BlocoNaoAdmitido, "O bloco não é admitido pela finalidade do formulário.");
                    break;
                case TipoEtapaFormulario.Bloco when !blocos.Add(etapa.Bloco):
                    Recusar($"{campo}.bloco", EstruturaFormularioErrorCodes.BlocoRepetido, "O bloco aparece mais de uma vez no formulário.");
                    break;
                case TipoEtapaFormulario.Nenhum:
                    Recusar($"{campo}.tipo", EstruturaFormularioErrorCodes.TipoDeEtapaObrigatorio, "A etapa é uma seção ou um bloco de sistema.");
                    break;
                default:
                    break;
            }
        }

        foreach (BlocoSistema exigido in BlocosExigidos(finalidade).Where(b => !blocos.Contains(b)).Order())
        {
            Recusar("etapas", EstruturaFormularioErrorCodes.BlocoExigidoAusente, $"O formulário precisa do bloco {ParaToken(exigido)}.");
        }

        EtapaEstrutura? ultima = etapas.MaxBy(static e => e.Ordem);
        if (blocos.Contains(BlocoSistema.RevisaoEAceite) && ultima?.Bloco != BlocoSistema.RevisaoEAceite)
        {
            Recusar("etapas", EstruturaFormularioErrorCodes.RevisaoEAceiteForaDoFim, "A revisão e aceite é sempre a última etapa do formulário.");
        }

        return erros;
    }

    /// <summary>
    /// Confere os itens contra as etapas: todo item está numa seção do formulário, e a ordem dos
    /// itens acompanha a ordem das seções — um item de seção posterior nunca vem antes. No
    /// rascunho (<paramref name="secaoObrigatoria"/> falso), o item ainda sem seção passa, e só a
    /// seção declarada é conferida; a recusa aponta sempre o índice do item na lista recebida.
    /// </summary>
    public static IReadOnlyList<FieldError> ValidarItens(
        IReadOnlyList<EtapaEstrutura> etapas, IReadOnlyList<ItemEstrutura> itens, bool secaoObrigatoria = true)
    {
        ArgumentNullException.ThrowIfNull(etapas);
        ArgumentNullException.ThrowIfNull(itens);

        Dictionary<string, EtapaEstrutura> secoes = etapas
            .Where(static e => e.Tipo == TipoEtapaFormulario.Secao)
            .GroupBy(static e => e.Codigo, StringComparer.Ordinal)
            .ToDictionary(static g => g.Key, static g => g.First(), StringComparer.Ordinal);

        List<FieldError> erros = [];
        for (int i = 0; i < itens.Count; i++)
        {
            if (itens[i].EtapaCodigo is not { } etapa ? secaoObrigatoria : !secoes.ContainsKey(etapa))
            {
                erros.Add(new($"itens[{i}].etapaCodigo", new DomainError(
                    EstruturaFormularioErrorCodes.ItemForaDeSecao, $"O item '{itens[i].FatoCodigo}' precisa estar numa seção do formulário.")));
            }
        }

        if (erros.Count > 0)
        {
            return erros;
        }

        ItemEstrutura? anterior = null;
        foreach (ItemEstrutura item in itens.Where(static i => i.EtapaCodigo is not null).OrderBy(static i => i.Ordem))
        {
            if (anterior is not null && secoes[item.EtapaCodigo!].Ordem < secoes[anterior.EtapaCodigo!].Ordem)
            {
                erros.Add(new("itens", new DomainError(
                    EstruturaFormularioErrorCodes.ItemForaDaOrdemDasSecoes,
                    $"O item '{item.FatoCodigo}' vem depois de '{anterior.FatoCodigo}', mas está numa seção anterior à dele.")));
                break;
            }

            anterior = item;
        }

        return erros;
    }

    public static string ParaToken(FinalidadeFormulario finalidade) => finalidade switch
    {
        FinalidadeFormulario.Inscricao => FinalidadeInscricao,
        FinalidadeFormulario.IsencaoTaxa => FinalidadeIsencaoTaxa,
        FinalidadeFormulario.Habilitacao => FinalidadeHabilitacao,
        _ => throw new ArgumentOutOfRangeException(nameof(finalidade), finalidade, "Finalidade sem token."),
    };

    /// <summary>A finalidade do token; token desconhecido é o sentinela.</summary>
    public static FinalidadeFormulario FinalidadeDoToken(string? token) => token switch
    {
        FinalidadeInscricao => FinalidadeFormulario.Inscricao,
        FinalidadeIsencaoTaxa => FinalidadeFormulario.IsencaoTaxa,
        FinalidadeHabilitacao => FinalidadeFormulario.Habilitacao,
        _ => FinalidadeFormulario.Nenhuma,
    };

    public static string ParaToken(TipoEtapaFormulario tipo) => tipo switch
    {
        TipoEtapaFormulario.Secao => TipoSecao,
        TipoEtapaFormulario.Bloco => TipoBloco,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de etapa sem token."),
    };

    public static TipoEtapaFormulario TipoDoToken(string? token) => token switch
    {
        TipoSecao => TipoEtapaFormulario.Secao,
        TipoBloco => TipoEtapaFormulario.Bloco,
        _ => TipoEtapaFormulario.Nenhum,
    };

    public static string? ParaToken(BlocoSistema bloco) => bloco switch
    {
        BlocoSistema.Nenhum => null,
        BlocoSistema.ComprovacaoDocumental => BlocoComprovacaoDocumental,
        BlocoSistema.ModalidadesCalculadas => BlocoModalidadesCalculadas,
        BlocoSistema.RevisaoEAceite => BlocoRevisaoEAceite,
        _ => throw new ArgumentOutOfRangeException(nameof(bloco), bloco, "Bloco desconhecido."),
    };

    /// <summary>O bloco do token; ausente é seção, e token desconhecido também é o sentinela.</summary>
    public static BlocoSistema BlocoDoToken(string? token) => token switch
    {
        BlocoComprovacaoDocumental => BlocoSistema.ComprovacaoDocumental,
        BlocoModalidadesCalculadas => BlocoSistema.ModalidadesCalculadas,
        BlocoRevisaoEAceite => BlocoSistema.RevisaoEAceite,
        _ => BlocoSistema.Nenhum,
    };
}

/// <summary>Códigos de erro da estrutura do formulário.</summary>
public static class EstruturaFormularioErrorCodes
{
    public const string EtapaCodigoDuplicado = "EstruturaFormulario.EtapaCodigoDuplicado";
    public const string EtapaOrdemDuplicada = "EstruturaFormulario.EtapaOrdemDuplicada";
    public const string TipoDeEtapaObrigatorio = "EstruturaFormulario.TipoDeEtapaObrigatorio";
    public const string SecaoComBloco = "EstruturaFormulario.SecaoComBloco";
    public const string BlocoNaoAdmitido = "EstruturaFormulario.BlocoNaoAdmitido";
    public const string BlocoRepetido = "EstruturaFormulario.BlocoRepetido";
    public const string BlocoExigidoAusente = "EstruturaFormulario.BlocoExigidoAusente";
    public const string RevisaoEAceiteForaDoFim = "EstruturaFormulario.RevisaoEAceiteForaDoFim";
    public const string ItemForaDeSecao = "EstruturaFormulario.ItemForaDeSecao";
    public const string ItemForaDaOrdemDasSecoes = "EstruturaFormulario.ItemForaDaOrdemDasSecoes";
}
