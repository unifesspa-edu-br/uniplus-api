namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

/// <summary>Os códigos de recusa da prova presencial do processo (UNI-REQ-0407).</summary>
public static class ProvaPresencialErrorCodes
{
    /// <summary>O processo ainda não declara se tem prova presencial.</summary>
    public const string PorDeclarar = "ProvaPresencial.PorDeclarar";

    /// <summary>O processo declara prova presencial e nenhum município de aplicação.</summary>
    public const string SemMunicipio = "ProvaPresencial.SemMunicipio";

    /// <summary>O processo declara que não tem prova presencial e declara município de aplicação.</summary>
    public const string MunicipiosSemProvaPresencial = "ProvaPresencial.MunicipiosSemProvaPresencial";

    /// <summary>O município de aplicação já está declarado no processo.</summary>
    public const string MunicipioRepetido = "ProvaPresencial.MunicipioRepetido";

    /// <summary>O processo declara mais municípios de aplicação do que o limite técnico admite.</summary>
    public const string MunicipiosAcimaDoLimite = "ProvaPresencial.MunicipiosAcimaDoLimite";

    /// <summary>O Geo não respondeu, e os municípios de aplicação ainda não foram conferidos.</summary>
    public const string MunicipiosNaoConferidosNoGeo = "ProvaPresencial.MunicipiosNaoConferidosNoGeo";

    /// <summary>O processo tem prova presencial, e nenhum formulário da inscrição pede a cidade de prova a todo candidato.</summary>
    public const string InscricaoSemCidadeDeProva = "ProcessoSeletivo.InscricaoSemCidadeDeProva";

    /// <summary>Um formulário pede a cidade de prova, mas o processo não declara prova presencial.</summary>
    public const string CidadeDeProvaSemProvaPresencial = "ProcessoSeletivo.CidadeDeProvaSemProvaPresencial";
}
