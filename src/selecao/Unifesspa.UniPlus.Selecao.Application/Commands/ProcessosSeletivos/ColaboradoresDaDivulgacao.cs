namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Interfaces;

/// <summary>
/// Os repositórios e serviços que a divulgação do certame usa, reunidos para o caso de uso trafegar
/// entre as etapas sem repetir uma lista de dependências em cada uma.
/// </summary>
/// <param name="ProcessosSeletivos">O repositório dos processos e de suas versões.</param>
/// <param name="CertamesDivulgados">O repositório da linha de divulgação.</param>
/// <param name="Codecs">O registro de codecs do envelope.</param>
/// <param name="AtosDoProcesso">O repositório do registro dos atos do processo.</param>
/// <param name="UnitOfWork">A unidade de trabalho que grava a divulgação.</param>
/// <param name="Relogio">O relógio.</param>
/// <param name="Copia">Os cadastros e o acervo de onde e para onde se copiam os documentos.</param>
internal sealed record ColaboradoresDaDivulgacao(
    IProcessoSeletivoRepository ProcessosSeletivos,
    ICertameDivulgadoRepository CertamesDivulgados,
    IRegistroCodecsEnvelope Codecs,
    IAtoDoProcessoRepository AtosDoProcesso,
    ISelecaoUnitOfWork UnitOfWork,
    TimeProvider Relogio,
    CopiaParaOAcervo Copia);
