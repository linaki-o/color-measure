using ColorMeasure.Application.Workflow;
using ColorMeasure.Domain.Abstractions;
using ColorMeasure.Infrastructure.CaSdk;
using ColorMeasure.Infrastructure.Display.GpuPalette;
using ColorMeasure.Infrastructure.Reporting;
using Microsoft.Extensions.DependencyInjection;

namespace ColorMeasure.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddColorMeasureInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ICA200Probe, Ca200ProbeAdapter>();
        services.AddSingleton<IDisplayDriver, GpuPaletteDisplayDriver>();
        services.AddSingleton<IXyDiagramRenderer, XyDiagramRenderer>();
        services.AddSingleton<CsvReportSink>();
        services.AddSingleton<XlsxReportSink>();
        services.AddSingleton<PdfReportSink>();
        services.AddSingleton<HtmlReportSink>();
        services.AddSingleton<IReportSink>(sp => new CompositeReportSink(new IReportSink[]
        {
            sp.GetRequiredService<CsvReportSink>(),
            sp.GetRequiredService<XlsxReportSink>(),
            sp.GetRequiredService<PdfReportSink>(),
            sp.GetRequiredService<HtmlReportSink>(),
        }));
        services.AddSingleton<IMeasurementWorkflow, MeasurementWorkflow>();
        return services;
    }
}
